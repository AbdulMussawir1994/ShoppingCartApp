using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SalesOrderApi.Helpers;
using System.Text;
using System.Text.Json;

namespace SalesOrderApi.Repository.RabbitMqProducer;

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly JsonSerializerOptions _serializerOptions;

    public RabbitMqService(IOptions<RabbitMqSettings> options)
    {
        if (options == null || options.Value == null)
        {
            throw new ArgumentNullException(nameof(options), "RabbitMQ settings are required.");
        }

        var factory = new ConnectionFactory
        {
            HostName = options.Value.Host,
            Port = options.Value.Port,
            UserName = options.Value.Username,
            Password = options.Value.Password
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.BasicQos(0, 1, false); // Ensure fair dispatch of messages
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to establish RabbitMQ connection.", ex);
        }
    }

    public void PublishMessage<T>(string queueName, T message)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));

        if (message == null)
            throw new ArgumentNullException(nameof(message), "Message cannot be null.");

        try
        {
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
        }
        catch (Exception ex)
        {
            // Log exception (consider using a logging framework)
            throw new InvalidOperationException("Failed to publish message to RabbitMQ.", ex);
        }
    }

    public bool PublishMessageWithReturn<T>(string queueName, T message)
    {
        if (string.IsNullOrWhiteSpace(queueName) || message == null)
            return false;

        try
        {
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();

        _channel?.Dispose();
        _connection?.Dispose();
    }
}
