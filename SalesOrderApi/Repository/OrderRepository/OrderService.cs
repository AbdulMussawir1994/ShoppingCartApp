using Mapster;
using Microsoft.EntityFrameworkCore;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Dtos;
using SalesOrderApi.Model;
using SalesOrderApi.Repository.UserContext;
using SalesOrderApi.Utilities;

namespace SalesOrderApi.Repository.OrderRepository
{
    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _db;
        private readonly IUserService _contextUser;
        //private readonly IRabbitMqService _rabbitMqService;
        private readonly IHttpClientFactory _httpClientFactory;

        public OrderService(OrderDbContext db, IUserService contextUser, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _contextUser = contextUser;
            // _rabbitMqService = rabbitMqService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<MobileResponse<IEnumerable<GetOrderDto>>> GetAllAsync(CancellationToken ctx)
        {
            var orders = await _db.Orders.AsNoTracking().ToListAsync(ctx);
            return orders.Any()
                ? MobileResponse<IEnumerable<GetOrderDto>>.Success(orders.Adapt<IEnumerable<GetOrderDto>>(), "Orders Fetched")
                : MobileResponse<IEnumerable<GetOrderDto>>.EmptySuccess(Enumerable.Empty<GetOrderDto>(), "No Orders Found");
        }

        public async Task<MobileResponse<GetOrderDto>> GetByIdAsync(int id, CancellationToken ctx)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id, ctx);
            return order is null
                ? MobileResponse<GetOrderDto>.Fail("Order Not Found")
                : MobileResponse<GetOrderDto>.Success(order.Adapt<GetOrderDto>(), "Order Fetched");
        }

        public async Task<MobileResponse<GetOrderDto>> CreateAsync(CreateOrderDto model, CancellationToken ctx)
        {
            var order = model.Adapt<Order>();

            var client = _httpClientFactory.CreateClient("ProductApi");

            // Assuming the base URL is already set, only append relative path
            var productResponse = await client.GetAsync($"products/{model.ProductId}", ctx);

            if (!productResponse.IsSuccessStatusCode)
            {
                return MobileResponse<GetOrderDto>.Fail("Failed to fetch product details from external service.");
            }

            var productContent = await productResponse.Content.ReadAsStringAsync(ctx);
            //var productData = JsonSerializer.Deserialize<ProductDto>(productContent, new JsonSerializerOptions
            //{
            //    PropertyNameCaseInsensitive = true
            //});

            //if (productData == null)
            //{
            //    return MobileResponse<GetOrderDto>.Fail("Product data was empty.");
            //}

            // ✅ Populate order with external data
            order.UserId = _contextUser?.UserId ?? "1";
            order.Status = "Pending";
            //   order.ProductName = productData.ProductName;
            order.Consumer = _contextUser?.Email ?? "Not Found";

            var rabbitMq = new OrderMessageDto
            {
                OrderId = order.OrderId,
                ProductId = order.ProductId,
                UserId = order.UserId,
                ProductName = order.ProductName,
                Consumer = order.Consumer,
                CreatedDate = order.CreatedDate,
                Status = order.Status,
                TotalOrders = order.TotalOrders,
            };

            //  _rabbitMqService.PublishMessage("OrderQueue", rabbitMq);

            await _db.Orders.AddAsync(order, ctx);
            var result = await _db.SaveChangesAsync(ctx);
            return result > 0
                ? MobileResponse<GetOrderDto>.Success(order.Adapt<GetOrderDto>(), "Order Created")
                : MobileResponse<GetOrderDto>.Fail("Creation Failed");
        }

        //public async Task<MobileResponse<bool>> AutoCreateAsync(ProductDto model)
        //{
        //    var order = new OrderDetails
        //    {
        //        ProductId = model.ProductId,
        //        ProductName = model.ProductName,
        //        //   Stock = model.Quantity,
        //        Consumer = model.Consumer ?? "Unknown Customer",
        //        Status = "Confirmed",
        //        CreatedDate = DateTime.UtcNow,
        //        //   Price = model.ProductPrice,
        //        UserId = model.User,
        //    };

        //    await _db.OrderDetails.AddAsync(order);
        //    var result = await _db.SaveChangesAsync();

        //    return result > 0
        //        ? MobileResponse<bool>.Success(true, "Order Created")
        //        : MobileResponse<bool>.Fail("Creation Failed");
        //}

        public async Task<MobileResponse<GetOrderDto>> UpdateAsync(int id, CreateOrderDto model, CancellationToken ctx)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ctx);
            if (order == null) return MobileResponse<GetOrderDto>.Fail("Order Not Found");

            order.ProductId = model.ProductId;
            // order.ProductName = model.ProductName;
            //    order.Stock = model.Stock;
            //   order.Price = model.Price;

            _db.Orders.Update(order);
            var result = await _db.SaveChangesAsync(ctx);
            return result > 0
                ? MobileResponse<GetOrderDto>.Success(order.Adapt<GetOrderDto>(), "Order Updated")
                : MobileResponse<GetOrderDto>.Fail("Update Failed");
        }

        public async Task<MobileResponse<bool>> DeleteAsync(int id, CancellationToken ctx)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ctx);
            if (order == null) return MobileResponse<bool>.Fail("Order Not Found");

            _db.Orders.Remove(order);
            var result = await _db.SaveChangesAsync(ctx);
            return result > 0
                ? MobileResponse<bool>.EmptySuccess(true, "Order Deleted")
                : MobileResponse<bool>.Fail("Delete Failed");
        }
    }
}
