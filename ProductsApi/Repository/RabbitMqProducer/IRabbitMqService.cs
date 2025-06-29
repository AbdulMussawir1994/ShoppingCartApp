namespace ProductsApi.Repository.RabbitMqProducer;

public interface IRabbitMqService
{
    void PublishMessage<T>(string queueName, T message);
    bool PublishMessageWithReturn<T>(string queueName, T message);
}
