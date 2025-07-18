using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using ProductsApi.DbContextClass;
using ProductsApi.Dtos;
using ProductsApi.Repository.ProductRepository;
using ProductsApi.Repository.RabbitMqProducer;
using ProductsApi.Repository.UserContext;

namespace TestingMicroservices.RepositoryTest.Product;

public class ProductServiceTests
{
    private readonly ProductDbContext _dbContext;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IRabbitMqService> _mockRabbitMqService;
    private readonly Mock<IDistributedCache> _dis;
    private readonly Mock<IConfiguration> _config;
    //  private readonly Mock<SnowflakeIdGenerator> _mockSnake;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Use unique DB for isolation
            .Options;

        _dbContext = new ProductDbContext(options);
        _mockUserService = new Mock<IUserService>();
        _mockRabbitMqService = new Mock<IRabbitMqService>();
        _dis = new Mock<IDistributedCache>();
        _config = new Mock<IConfiguration>();
        //_mockSnake = new Mock<SnowflakeIdGenerator>();

        _service = new ProductService(_dbContext, _mockUserService.Object, _mockRabbitMqService.Object, _dis.Object, _config.Object);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenNoProductsExist()
    {
        var ctx = CancellationToken.None;

        // Clear any products
        _dbContext.Products.RemoveRange(_dbContext.Products);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAllAsync(ctx);

        result.Status.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("No Products Found");
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateProductSuccessfully()
    {
        var ctx = CancellationToken.None;
        var model = new CreateProductDto
        {
            ProductName = "Test Product",
            ProductPrice = 99.99M,
            ProductCategory = "A+",
            ProductDescription = "N/A"
        };

        _mockUserService.Setup(x => x.UserId).Returns("1");
        //_mockRabbitMqService
        //  .Setup(x => x.PublishMessageWithReturn(It.IsAny<string>(), It.IsAny<object>()))
        //  .Returns(true); // <-- ✅ return a bool, not a Task

        var result = await _service.CreateAsync(model, ctx);

        result.Status.Should().BeTrue();
        result.Data.product.Should().NotBeEmpty();
        result.Message.Should().Be("Product Created Successfully.");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        var ctx = CancellationToken.None;

        var result = await _service.DeleteAsync("non-existent-id", ctx);

        result.Status.Should().BeFalse();
        result.Message.Should().Be("Product Not Found");
    }

}
