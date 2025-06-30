using Mapster;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Dtos;
using SalesOrderApi.Dtos.Address;
using SalesOrderApi.Dtos.Product;
using SalesOrderApi.Model;
using SalesOrderApi.Repository.RabbitMqProducer;
using SalesOrderApi.Repository.UserContext;
using SalesOrderApi.Utilities;
using SalesOrderApi.ViewModels;
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
                ? MobileResponse<IEnumerable<GetOrderDto>>.SuccessT(orders.Adapt<IEnumerable<GetOrderDto>>(), "Orders Fetched")
                : MobileResponse<IEnumerable<GetOrderDto>>.SuccessT(Enumerable.Empty<GetOrderDto>(), "No Orders Found");
        }

        public async Task<MobileResponse<GetOrderDto>> GetByIdAsync(int id, CancellationToken ctx)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id, ctx);
            return order is null
                ? MobileResponse<GetOrderDto>.Fail("Order Not Found")
                : MobileResponse<GetOrderDto>.SuccessT(order.Adapt<GetOrderDto>(), "Order Fetched");
        }


        public async Task<MobileResponse<OrderMessageDto>> ProductConfirmAsync(ConfirmProductViewModel model)
        {
            var (connection, channel) = CreateRabbitMqChannel();

            using (connection)
            using (channel)
            {
                const string queueName = "OrderQueue";
                channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                channel.BasicQos(0, 1, false);

                var buffer = new List<ReadOnlyMemory<byte>>();
                bool isConfirmed = false;

                // 👇 Create execution strategy for retry-aware transaction
                var strategy = _db.Database.CreateExecutionStrategy();

                try
                {
                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var transaction = await _db.Database.BeginTransactionAsync();

                        while (true)
                        {
                            var result = channel.BasicGet(queue: queueName, autoAck: false);
                            if (result is null)
                                break;

                            var body = result.Body.ToArray();
                            var orderMessage = JsonSerializer.Deserialize<OrderMessageDto>(
                                Encoding.UTF8.GetString(body),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (orderMessage?.OrderId == model.OrderId)
                            {
                                var order = new Order
                                {
                                    OrderId = model.OrderId,
                                    Consumer = orderMessage.Consumer,
                                    UserId = orderMessage.UserId,
                                    Status = "Pending",
                                    CreatedDate = DateTime.UtcNow,
                                    ProductId = orderMessage.ProductId,
                                    ProductName = orderMessage.ProductName,
                                    TotalOrders = orderMessage.TotalOrders,
                                };

                                await _db.Orders.AddAsync(order);
                                var saved = await _db.SaveChangesAsync();

                                if (saved > 0)
                                {
                                    await transaction.CommitAsync();
                                    channel.BasicAck(result.DeliveryTag, false);
                                    isConfirmed = true;
                                    return; // Exit ExecuteAsync
                                }
                                else
                                {
                                    await transaction.RollbackAsync();
                                    channel.BasicAck(result.DeliveryTag, false);
                                    channel.BasicPublish(exchange: "", routingKey: queueName, body: body);
                                    throw new InvalidOperationException($"❌ DB failed to confirm Order {model.OrderId}.");
                                }
                            }

                            buffer.Add(body);
                            channel.BasicAck(result.DeliveryTag, false);
                        }

                        await transaction.CommitAsync();
                    });

                    // Re-publish messages back if not confirmed
                    if (!isConfirmed)
                    {
                        foreach (var msg in buffer)
                        {
                            channel.BasicPublish(exchange: "", routingKey: queueName, body: msg);
                        }
                        return MobileResponse<OrderMessageDto>.Fail($"❌ Order {model.OrderId} not found in '{queueName}'.");
                    }

                    return MobileResponse<OrderMessageDto>.Success($"✅ Order {model.OrderId} confirmed and removed from '{queueName}'.");
                }
                catch (Exception ex)
                {
                    // Rollback handled inside strategy block
                    return MobileResponse<OrderMessageDto>.Fail($"🚫 Error confirming order: {ex.Message}");
                }
            }
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
                CreatedDate = DateTime.UtcNow,
                DeliveryStatus = "Pending"
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

            return MobileResponse<OrderMessageDto>.SuccessT(rabbitMqOrder, "Order Created and Publish Message in to Confirmation Order");
        }

        public async Task<MobileResponse<string>> ConfirmOrderByIdAsync(ConfirmOrderViewModel model)
        {
            var (connection, channel) = CreateRabbitMqChannel();

            using (connection)
            using (channel)
            {
                try
                {
                    var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == model.OrderId);
                    if (order is null)
                    {
                        return MobileResponse<string>.Fail($"❌ Order {model.OrderId} not found.");
                    }

                    var isConfirmed = await ConfirmOrderByIdAsync(order);
                    if (!isConfirmed)
                    {
                        return MobileResponse<string>.Fail($"✅ Order {model.OrderId} Failed due to Error.");
                    }

                    var deliveryStatus = await ConfirmDeliveryStatus(model, order.Consumer, order.UserId);
                    if (deliveryStatus.Status)
                    {
                        return MobileResponse<string>.Success($"✅ Order {model.OrderId} confirmed.");
                    }

                    return MobileResponse<string>.Fail($"❌ Delivery failed for Order {model.OrderId}: {deliveryStatus.Message}");
                }
                catch (Exception ex)
                {
                    return MobileResponse<string>.Fail($"🚫 Error confirming order: {ex.Message}");
                }
            }
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
                ? MobileResponse<GetOrderDto>.SuccessT(order.Adapt<GetOrderDto>(), "Order Updated")
                : MobileResponse<GetOrderDto>.Fail("Update Failed");
        }

        public async Task<MobileResponse<bool>> DeleteAsync(int id, CancellationToken ctx)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ctx);
            if (order == null) return MobileResponse<bool>.Fail("Order Not Found");

            _db.Orders.Remove(order);
            var result = await _db.SaveChangesAsync(ctx);
            return result > 0
                ? MobileResponse<bool>.SuccessT(true, "Order Deleted")
                : MobileResponse<bool>.Fail("Delete Failed");
        }

        //public async Task<MobileResponse<string>> ConfirmOrderByIdInQueueAsync(ConfirmOrderViewModel model)
        //{
        //    var (connection, channel) = CreateRabbitMqChannel();

        //    using (connection)
        //    using (channel)
        //    {
        //        const string queueName = "OrderQueue";

        //        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        //        channel.BasicQos(0, 1, false);

        //        var buffer = new List<ReadOnlyMemory<byte>>();
        //        bool isConfirmed = false;

        //        while (true)
        //        {
        //            var result = channel.BasicGet(queue: queueName, autoAck: false);
        //            if (result is null)
        //                break;

        //            var body = result.Body.ToArray();
        //            var orderMessage = JsonSerializer.Deserialize<OrderMessageDto>(
        //                Encoding.UTF8.GetString(body),
        //                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        //            );

        //            if (orderMessage?.OrderId == model.OrderId)
        //            {
        //                bool orderConfirmed = await ConfirmOrderByIdAsync(model.OrderId);
        //                if (!orderConfirmed)
        //                {
        //                    channel.BasicAck(result.DeliveryTag, false);
        //                    return MobileResponse<string>.Fail($"⚠️ Order {model.OrderId} found but confirmation failed.");
        //                }

        //                var deliveryStatus = await ConfirmDeliveryStatus(model, orderMessage.Consumer, orderMessage.UserId);
        //                if (deliveryStatus.Status)
        //                {
        //                    channel.BasicAck(result.DeliveryTag, false);
        //                    isConfirmed = true;
        //                    return MobileResponse<string>.Success($"✅ Order {model.OrderId} confirmed and removed from '{queueName}'.");
        //                }
        //                else
        //                {
        //                    // Requeue failed delivery message
        //                    channel.BasicAck(result.DeliveryTag, false);
        //                    channel.BasicPublish(exchange: "", routingKey: queueName, body: body);
        //                    return MobileResponse<string>.Fail($"❌ Delivery failed for Order {model.OrderId} due to {deliveryStatus.Message}.");
        //                }
        //            }

        //            buffer.Add(body);
        //            channel.BasicAck(result.DeliveryTag, false);
        //        }

        //        // Requeue buffered unmatched messages
        //        foreach (var msg in buffer)
        //        {
        //            channel.BasicPublish(exchange: "", routingKey: queueName, body: msg);
        //        }

        //        return isConfirmed
        //            ? MobileResponse<string>.Success($"✅ Order {model.OrderId} confirmed.")
        //            : MobileResponse<string>.Fail($"❌ Order {model.OrderId} not found in '{queueName}'.");
        //    }
        //}

        public async Task<MobileResponse<ShippingResponseDto>> ConfirmDeliveryStatus(ConfirmOrderViewModel model, string consumerName, string userId)
        {
            var order = await _db.ConfirmOrders
                .Where(o => o.OrderId == model.OrderId)
                .FirstOrDefaultAsync();

            if (order is null)
            {
                return MobileResponse<ShippingResponseDto>.Fail("Order not found.");
            }

            // Update delivery status only if necessary
            if (!string.Equals(order.DeliveryStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                order.DeliveryStatus = "Confirmed";
                await _db.SaveChangesAsync();
            }

            var shippingDto = new ShippingResponseDto
            {
                UserId = userId,
                Consumer = consumerName,
                OrderId = model.OrderId,
                Address = model.Address,
            };

            var published = _rabbitMqService.PublishMessageWithReturn("ShippingQueue", shippingDto);

            return published
                ? MobileResponse<ShippingResponseDto>.SuccessT(shippingDto, "Shipping details sent to the supplier.")
                : MobileResponse<ShippingResponseDto>.Fail("Failed to send delivery status.");
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

        //private async Task<bool> ConfirmOrderByIdAsync(long orderId)
        //{
        //    var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);

        //    if (order is null)
        //        return false;

        //    if (order.Status == "Confirmed")
        //        return true;

        //    order.Status = "Confirmed";

        //    return await _db.SaveChangesAsync() > 0;
        //}

        private async Task<bool> ConfirmOrderByIdAsync(Order order)
        {
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    if (await _db.ConfirmOrders.AnyAsync(x => x.OrderId == order.OrderId))
                    {
                        return true; // Already confirmed
                    }

                    var confirmOrder = new ConfirmOrder
                    {
                        OrderId = order.OrderId,
                        UserId = order.UserId,
                        CreatedDate = DateTime.UtcNow,
                        DeliveryStatus = "Pending"
                    };

                    await _db.ConfirmOrders.AddAsync(confirmOrder);

                    if (order.Status != "Confirmed")
                    {
                        order.Status = "Confirmed";
                        _db.Orders.Update(order);
                    }

                    var rowsAffected = await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return rowsAffected > 0;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            });
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
