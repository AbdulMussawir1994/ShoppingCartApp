using Mapster;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Dtos;
using SalesOrderApi.Dtos.Product;
using SalesOrderApi.Model;
using SalesOrderApi.Repository.RabbitMqProducer;
using SalesOrderApi.Repository.UserContext;
using SalesOrderApi.Utilities;
using System.Text;
using System.Text.Json;

namespace SalesOrderApi.Repository.OrderRepository
{
    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _db;
        private readonly IUserService _contextUser;
        private readonly IRabbitMqService _rabbitMqService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public OrderService(OrderDbContext db, IUserService contextUser, IRabbitMqService rabbitMqService,
                                                IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _db = db;
            _contextUser = contextUser;
            _rabbitMqService = rabbitMqService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

            order.UserId = _contextUser?.UserId ?? "1";
            order.Status = "Pending";
            order.Consumer = _contextUser?.Email ?? "Not Found";

            var client = _httpClientFactory.CreateClient("ProductApi");
            var response = await client.GetAsync($"GetProductName/{model.ProductId}", ctx);

            if (!response.IsSuccessStatusCode)
                return MobileResponse<GetOrderDto>.Fail("Failed to fetch product details.");

            var content = await response.Content.ReadAsStringAsync(ctx);
            var product = JsonSerializer.Deserialize<MobileResponse<ProductDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })?.Data;

            if (product is null)
                return MobileResponse<GetOrderDto>.Fail("Product data was empty.");

            order.ProductName = product.ProductName;

            await _db.Orders.AddAsync(order, ctx);
            var saved = await _db.SaveChangesAsync(ctx);

            if (saved > 0)
            {
                _rabbitMqService.PublishMessage("OrderQueue", new OrderMessageDto
                {
                    OrderId = order.OrderId,
                    ProductId = order.ProductId,
                    UserId = order.UserId,
                    ProductName = order.ProductName,
                    Consumer = order.Consumer,
                    CreatedDate = order.CreatedDate,
                    Status = order.Status,
                    TotalOrders = order.TotalOrders,
                });

                return MobileResponse<GetOrderDto>.Success(order.Adapt<GetOrderDto>(), "Order Created");
            }

            return MobileResponse<GetOrderDto>.Fail("Creation Failed");
        }

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

        public async Task<string> ConfirmOrderByIdInQueueAsync(int OrderId)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"]
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            const string queueName = "OrderQueue";
            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            channel.BasicQos(0, 1, false);

            var tempQueue = $"{queueName}_temp_{Guid.NewGuid()}";
            channel.QueueDeclare(tempQueue, durable: true, exclusive: false, autoDelete: true);

            bool found = false;

            while (true)
            {
                var result = channel.BasicGet(queue: queueName, autoAck: false);
                if (result == null) break;

                var messageJson = Encoding.UTF8.GetString(result.Body.ToArray());

                var orderMessage = JsonSerializer.Deserialize<OrderMessageDto>(messageJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderMessage?.OrderId == OrderId)
                {
                    // ✅ Confirm the order via service
                    var success = await ConfirmOrderByIdAsync(OrderId);
                    if (success)
                    {
                        channel.BasicAck(result.DeliveryTag, false);
                        found = true;
                        break;
                    }
                    else
                    {
                        channel.BasicAck(result.DeliveryTag, false); // avoid stuck message
                        return $"⚠️ Order {OrderId} found but confirmation failed.";
                    }
                }

                // ❗ Push unmatched message to temp queue (requeue)
                channel.BasicAck(result.DeliveryTag, false);
                channel.BasicPublish(exchange: "", routingKey: tempQueue, body: result.Body);
            }

            // 🚚 Move temp queue messages back to original
            while (true)
            {
                var tempResult = channel.BasicGet(queue: tempQueue, autoAck: false);
                if (tempResult == null) break;
                channel.BasicAck(tempResult.DeliveryTag, false);
                channel.BasicPublish(exchange: "", routingKey: queueName, body: tempResult.Body);
            }

            return found
                ? $"✅ Order {OrderId} confirmed and removed from '{queueName}'."
                : $"❌ Order {OrderId} not found in '{queueName}'.";
        }

        public async Task<string> CleanupTempQueueAsync(string queueName)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"]
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            try
            {
                uint removedCount = 0;

                while (true)
                {
                    var result = channel.BasicGet(queue: queueName, autoAck: false);
                    if (result == null)
                        break;

                    channel.BasicAck(result.DeliveryTag, false);
                    removedCount++;
                }

                return $"🧹 Cleaned up {removedCount} messages from queue '{queueName}'.";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to clean up queue '{queueName}': {ex.Message}";
            }
        }

        private async Task<bool> ConfirmOrderByIdAsync(int orderId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId);
            if (order == null || order.Status == "Confirmed") return false;

            order.Status = "Confirmed";
            //  order.ConfirmedDate = DateTime.UtcNow;

            _db.Orders.Update(order);
            return await _db.SaveChangesAsync() > 0;
        }

        private (IConnection connection, IModel channel) CreateRabbitMqChannel()
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"]
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            return (connection, channel);
        }
    }
}
