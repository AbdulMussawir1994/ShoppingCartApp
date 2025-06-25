using Mapster;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
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

        public async Task<MobileResponse<OrderMessageDto>> CreateAsync(CreateOrderDto model, CancellationToken ctx)
        {
            var userId = _contextUser?.UserId ?? "1";
            var consumer = _contextUser?.Email ?? "Not Found";

            // 🔄 Map incoming DTO to entity early
            var order = model.Adapt<Order>();
            order.UserId = userId;
            order.Status = "Pending";
            order.Consumer = consumer;

            // ✅ Fetch product using HttpClient
            var client = _httpClientFactory.CreateClient("ProductApi");
            using var response = await client.GetAsync($"GetProductName/{model.ProductId}", ctx);

            if (!response.IsSuccessStatusCode)
                return MobileResponse<OrderMessageDto>.Fail("Failed to fetch product details.", "400");

            await using var contentStream = await response.Content.ReadAsStreamAsync(ctx);
            var productResponse = await JsonSerializer.DeserializeAsync<MobileResponse<ProductDto>>(contentStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, ctx);

            if (productResponse?.Data is not { } product)
                return MobileResponse<OrderMessageDto>.Fail("Product data was empty.", "404");

            // ✅ Assign product name
            order.ProductName = product.ProductName;

            // ✅ Save Order first so OrderId is generated
            await _db.Orders.AddAsync(order, ctx);
            var saved = await _db.SaveChangesAsync(ctx);

            if (saved == 0)
                return MobileResponse<OrderMessageDto>.Fail("Order creation failed.");

            // ✅ Now insert ConfirmOrder with correct OrderId
            var confirmOrder = new ConfirmOrder
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                CreatedDate = DateTime.UtcNow
            };

            await _db.ConfirmOrders.AddAsync(confirmOrder, ctx);
            await _db.SaveChangesAsync(ctx);

            // ✅ Publish message to RabbitMQ
            var rabbitMqOrder = new OrderMessageDto
            {
                OrderId = order.OrderId,
                ProductId = order.ProductId,
                UserId = userId,
                ProductName = product.ProductName,
                Consumer = consumer,
                CreatedDate = order.CreatedDate,
                Status = order.Status,
                TotalOrders = order.TotalOrders,
            };

            _rabbitMqService.PublishMessage("OrderQueue", rabbitMqOrder);

            //_rabbitMqService.PublishMessage("OrderQueue", new OrderMessageDto
            //{
            //    OrderId = order.OrderId,
            //    ProductId = order.ProductId,
            //    UserId = userId,
            //    ProductName = product.ProductName,
            //    Consumer = consumer,
            //    CreatedDate = order.CreatedDate,
            //    Status = order.Status,
            //    TotalOrders = order.TotalOrders,
            //});

            return MobileResponse<OrderMessageDto>.Success(rabbitMqOrder, "Order Created and Publish Message in to Confirmation Order");
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

        public async Task<string> ConfirmOrderByIdInQueueAsync(int orderId)
        {
            var (connection, channel) = CreateRabbitMqChannel();
            using (connection)
            using (channel)
            {
                const string queueName = "OrderQueue";

                // Ensure queue exists
                channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                channel.BasicQos(0, 1, false);

                var buffer = new List<ReadOnlyMemory<byte>>();
                bool found = false;

                while (true)
                {
                    var result = channel.BasicGet(queue: queueName, autoAck: false);
                    if (result is null) break;

                    var body = result.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var orderMessage = JsonSerializer.Deserialize<OrderMessageDto>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (orderMessage?.OrderId == orderId)
                    {
                        var confirmed = await ConfirmOrderByIdAsync(orderId);
                        if (confirmed)
                        {
                            channel.BasicAck(result.DeliveryTag, false);
                            found = true;
                            break;
                        }
                        else
                        {
                            // Prevent stuck message even on failure
                            channel.BasicAck(result.DeliveryTag, false);
                            return $"⚠️ Order {orderId} found but confirmation failed.";
                        }
                    }

                    // Buffer unmatched message
                    buffer.Add(body);
                    channel.BasicAck(result.DeliveryTag, false);
                }

                // Re-publish buffered messages back into the queue
                foreach (var msg in buffer)
                {
                    channel.BasicPublish(exchange: "", routingKey: queueName, body: msg);
                }

                return found
                    ? $"✅ Order {orderId} confirmed and removed from '{queueName}'."
                    : $"❌ Order {orderId} not found in '{queueName}'.";
            }
        }

        //public async Task<string> ConfirmOrderByIdWithNewQueue(int OrderId)
        //{
        //    var (connection, channel) = CreateRabbitMqChannel();
        //    using (connection)
        //    using (channel)
        //    {
        //        const string queueName = "OrderQueue";
        //        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        //        channel.BasicQos(0, 1, false);

        //        var tempQueue = $"{queueName}-{Guid.NewGuid()}";
        //        channel.QueueDeclare(tempQueue, durable: true, exclusive: false, autoDelete: true);

        //        bool found = false;

        //        while (true)
        //        {
        //            var result = channel.BasicGet(queue: queueName, autoAck: false);
        //            if (result is null) break;

        //            var messageJson = Encoding.UTF8.GetString(result.Body.ToArray());

        //            var orderMessage = JsonSerializer.Deserialize<OrderMessageDto>(messageJson, new JsonSerializerOptions
        //            {
        //                PropertyNameCaseInsensitive = true
        //            });

        //            if (orderMessage?.OrderId == OrderId)
        //            {
        //                // ✅ Confirm the order via service
        //                var success = await ConfirmOrderByIdAsync(OrderId, tempQueue);
        //                if (success)
        //                {
        //                    channel.BasicAck(result.DeliveryTag, false);
        //                    found = true;
        //                    break;
        //                }
        //                else
        //                {
        //                    channel.BasicAck(result.DeliveryTag, false); // avoid stuck message
        //                    return $"⚠️ Order {OrderId} found but confirmation failed.";
        //                }
        //            }

        //            // ❗ Push unmatched message to temp queue (requeue)
        //            channel.BasicAck(result.DeliveryTag, false);
        //            channel.BasicPublish(exchange: "", routingKey: tempQueue, body: result.Body);
        //        }

        //        // 🚚 Move temp queue messages back to original
        //        while (true)
        //        {
        //            var tempResult = channel.BasicGet(queue: tempQueue, autoAck: false);
        //            if (tempResult == null) break;
        //            channel.BasicAck(tempResult.DeliveryTag, false);
        //            channel.BasicPublish(exchange: "", routingKey: queueName, body: tempResult.Body);
        //        }

        //        return found
        //            ? $"✅ Order {OrderId} confirmed and removed from '{queueName}'."
        //            : $"❌ Order {OrderId} not found in '{queueName}'.";
        //    }
        //}

        public async Task<string> UpdateConfirmOrderDetails(string queueName)
        {
            try
            {
                var (connection, channel) = CreateRabbitMqChannel();

                using (connection)
                using (channel)
                {
                    var result = channel.QueueDeclarePassive(queueName); // Throws if queue doesn't exist
                    channel.QueueDelete(queueName);
                    return $"✅ Queue '{queueName}' (with {result.MessageCount} messages) deleted.";


                    // 🧹 Delete the queue — this automatically removes all messages
                    // channel.QueueDelete(queue: queueName, ifUnused: false, ifEmpty: false);
                    // return $"✅ Queue '{queueName}' deleted successfully.";
                }
            }
            catch (OperationInterruptedException)
            {
                return $"⚠️ Queue '{queueName}' does not exist.";
            }
        }

        private async Task<bool> ConfirmOrderByIdAsync(int orderId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId);

            if (order is null)
                return false;

            order.Status = "Confirmed";
            //    order.Queue = queueName;

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
