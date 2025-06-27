using Mapster;
using RabbitMQ.Client;
using ShippingOrderApi.DbContextClass;
using ShippingOrderApi.Dtos;
using ShippingOrderApi.Model;
using ShippingOrderApi.Repository.UserContext;
using ShippingOrderApi.Utilities;
using ShippingOrderApi.ViewModel;
using System.Text;
using System.Text.Json;

namespace ShippingOrderApi.Repository.SupplierRepository
{
    public class SupplierService : ISupplierService
    {
        private readonly ShippingDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        public SupplierService(ShippingDbContext db, IConfiguration configuration, IUserService userService)
        {
            _db = db;
            _configuration = configuration;
            _userService = userService;
        }

        public async Task<MobileResponse<string>> ConfirmDeliveryAsync(DispatchViewModel model)
        {
            var (connection, channel) = CreateRabbitMqChannel();

            using (connection)
            using (channel)
            {
                const string queueName = "ShippingQueue";
                string userId = _userService?.UserId ?? "1";

                channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                channel.BasicQos(0, 1, false);

                var unmatchedMessages = new List<ReadOnlyMemory<byte>>();

                while (true)
                {
                    var result = channel.BasicGet(queue: queueName, autoAck: false);
                    if (result is null) break;

                    var body = result.Body.ToArray();
                    var message = JsonSerializer.Deserialize<ShippingDto>(
                        Encoding.UTF8.GetString(body),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (message?.OrderId == model.OrderId && message.UserId == userId)
                    {
                        var isShipped = await ConfirmShippedByIdAsync(message, model.SupplierId);
                        channel.BasicAck(result.DeliveryTag, false);

                        if (isShipped)
                        {
                            return MobileResponse<string>.Success($"✅ Order {model.OrderId} confirmed and removed from '{queueName}'.");
                        }

                        // Ship failed
                        return MobileResponse<string>.Fail($"⚠️ Order {model.OrderId} found but confirmation failed.");
                    }

                    unmatchedMessages.Add(body);
                    channel.BasicAck(result.DeliveryTag, false);
                }

                // Requeue unmatched messages
                foreach (var msg in unmatchedMessages)
                {
                    channel.BasicPublish(exchange: "", routingKey: queueName, body: msg);
                }

                return MobileResponse<string>.Fail($"❌ Order {model.OrderId} not found in '{queueName}'.");
            }
        }

        public async Task<MobileResponse<SupplierGetDto>> CreateSupplierAsync(SupplierCreateDto model, CancellationToken ctx)
        {
            try
            {
                var supplier = model.Adapt<Supplier>();

                await _db.Suppliers.AddAsync(supplier, ctx);
                var result = await _db.SaveChangesAsync(ctx);

                return result > 0
                    ? MobileResponse<SupplierGetDto>.Success(supplier.Adapt<SupplierGetDto>(), "Supplier Created Successfully.")
                    : MobileResponse<SupplierGetDto>.Fail("Failed to Create Supplier");
            }
            catch (Exception ex)
            {
                return MobileResponse<SupplierGetDto>.Fail($"An error occurred: {ex.Message}");
            }
        }

        public async Task<bool> ConfirmShippedByIdAsync(ShippingDto model, int supplierId)
        {
            try
            {
                var shipping = new ShippingAddress
                {
                    Consumer = model.Consumer,
                    UserId = model.UserId,
                    OrderId = model.OrderId,
                    HomeAddress = model.Address.HomeAddress,
                    City = model.Address.City,
                    Region = model.Address.Region,
                    Country = model.Address.Country,
                    Phone = model.Address.Phone,
                    SupplierId = supplierId
                };

                await _db.ShippingAddresses.AddAsync(shipping);
                var result = await _db.SaveChangesAsync();

                return result > 0;
            }
            catch (Exception)
            {
                return false;
            }
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
