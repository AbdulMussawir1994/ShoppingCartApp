using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Dtos;
using SalesOrderApi.Dtos.Product;
using SalesOrderApi.Repository.OrderRepository;
using SalesOrderApi.Repository.RabbitMqProducer;
using SalesOrderApi.Repository.UserContext;
using SalesOrderApi.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;

namespace TestingMicroservices.RepositoryTest.Order;

public class OrderServiceTests
{
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly Mock<IRabbitMqService> _mockRabbitMqService = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly IConfiguration _configuration;
    private readonly DbContextOptions<OrderDbContext> _dbContextOptions;
    private readonly OrderDbContext _db;

    public OrderServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new OrderDbContext(_dbContextOptions);

        var inMemorySettings = new Dictionary<string, string> {
        {"RabbitMQ:Host", "localhost"},
        {"RabbitMQ:Port", "5672"},
        {"RabbitMQ:Username", "guest"},
        {"RabbitMQ:Password", "guest"}
    };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateOrderSuccessfully()
    {
        // Arrange
        var model = new CreateOrderDto
        {
            ProductId = "prod-123",
            TotalOrders = 5
        };

        var productResponse = new MobileResponse<ProductDto>
        {
            Status = true,
            Data = new ProductDto { ProductName = "Mock Product" }
        };

        var jsonResponse = JsonSerializer.Serialize(productResponse);
        var httpMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var mockMessageHandler = new Mock<HttpMessageHandler>();

        mockMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpMessage);

        var httpClient = new HttpClient(mockMessageHandler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("ProductApi")).Returns(httpClient);

        _mockUserService.Setup(x => x.UserId).Returns("user-1");
        _mockUserService.Setup(x => x.Email).Returns("test@example.com");

        _mockRabbitMqService.Setup(x => x.PublishMessage(It.IsAny<string>(), It.IsAny<object>()));

        var service = new OrderService(_db, _mockUserService.Object, _mockRabbitMqService.Object, factory.Object, _configuration);

        // Act
        var result = await service.CreateAsync(model, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
        result.Message.Should().Contain("Order Created");
        result.Data.Should().NotBeNull();
        result.Data.OrderId.Should().NotBeNullOrEmpty();
        result.Data.ProductName.Should().Be("Mock Product");

        var savedOrder = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == result.Data.OrderId);
        savedOrder.Should().NotBeNull();
        savedOrder.Status.Should().Be("Pending");
    }
}
