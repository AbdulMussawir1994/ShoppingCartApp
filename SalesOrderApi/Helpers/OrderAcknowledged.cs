using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SalesOrderApi.Dtos.Product;
using SalesOrderApi.Repository.OrderRepository;
using System.Text;
using System.Text.Json;

namespace SalesOrderApi.Helpers;

public class OrderAcknowledged : BackgroundService
{
    private readonly IOrderService _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderAcknowledged> _logger;
    private IConnection _connection;
    private IModel _channel;

    public OrderAcknowledged(IOrderService serviceProvider, IConfiguration configuration, ILogger<OrderAcknowledged> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderAcknowledged is starting...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"],
                DispatchConsumersAsync = true // Enables async event handlers
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: "ProductQueue",
                durable: true,
                exclusive: false,
                autoDelete: false);

            _logger.LogInformation("RabbitMQ connection and channel established successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OrderAcknowledged.");
            throw;
        }

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("OrderAcknowledged is stopping."));

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                _logger.LogInformation($"Received raw JSON: {json}");

                var message = JsonSerializer.Deserialize<ProductDto>(body);
                if (message == null)
                {
                    _logger.LogWarning("Received a null or malformed message.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                _logger.LogInformation($"Processing product: {message.ProductName} (ID: {message.Id})");

                //    await ProcessOrderAsync(message);

                _channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation($"Successfully processed product: {message.ProductName}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Failed to deserialize message. Raw JSON: {Encoding.UTF8.GetString(ea.Body.ToArray())}");
                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the message.");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(queue: "ProductQueue", autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    //private async Task ProcessOrderAsync(ProductDto product)
    //{
    //    await _serviceProvider.AutoCreateAsync(product);
    //}

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderAcknowledged is stopping...");

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
