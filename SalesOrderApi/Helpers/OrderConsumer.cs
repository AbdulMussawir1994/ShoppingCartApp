using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Dtos.Product;
using SalesOrderApi.Model;
using System.Text;
using System.Text.Json;

namespace SalesOrderApi.Helpers;

public class OrderConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<OrderConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderConsumer is starting...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"],
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: "ProductQueue",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            // Recommended to avoid overloading the consumer
            _channel.BasicQos(0, 10, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    _logger.LogInformation($"Received raw JSON: {json}");

                    var product = JsonSerializer.Deserialize<ProductDto>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (product == null)
                    {
                        _logger.LogWarning("Received a null or malformed message.");
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }

                    _logger.LogInformation($"Processing product: {product.ProductName} (ID: {product.Id})");

                    await ProcessOrderAsync(product, cancellationToken);

                    _channel.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation($"Successfully processed product: {product.ProductName}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Failed to deserialize message. Raw JSON: {Encoding.UTF8.GetString(ea.Body.ToArray())}");
                    _channel.BasicNack(ea.DeliveryTag, false, false); // Don't requeue
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing the message.");
                    _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue for retry
                }
            };

            // ✅ IMPORTANT: Subscribe the consumer immediately during StartAsync
            _channel.BasicConsume(queue: "ProductQueue", autoAck: false, consumer: consumer);

            _logger.LogInformation("RabbitMQ consumer subscribed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ connection or consumer.");
            throw;
        }

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nothing to do here because we already subscribed in StartAsync
        return Task.CompletedTask;
    }

    private async Task ProcessOrderAsync(ProductDto product, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var order = new Order
        {
            ProductId = product.Id,
            ProductName = product.ProductName,
            //    Stock = product.Quantity,
            //     Consumer = product.Consumer ?? "Unknown Customer",
            Status = "Confirmed",
            CreatedDate = DateTime.UtcNow,
            //Price = product.ProductPrice,
            //    UserId = product.User,
        };

        await dbContext.Orders.AddAsync(order, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderConsumer is stopping...");

        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while closing RabbitMQ resources.");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }

        return base.StopAsync(cancellationToken);
    }
}
