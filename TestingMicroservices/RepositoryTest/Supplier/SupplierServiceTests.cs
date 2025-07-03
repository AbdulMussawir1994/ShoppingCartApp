using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using ShippingOrderApi.DbContextClass;
using ShippingOrderApi.Dtos;
using ShippingOrderApi.Model;
using ShippingOrderApi.Repository.SupplierRepository;
using ShippingOrderApi.Repository.UserContext;

namespace TestingMicroservices.RepositoryTest.Supplier;

public class SupplierServiceTests
{
    private readonly SupplierService _service;
    private readonly Mock<IUserService> _mockUserService;
    private readonly IConfiguration _configuration;
    private readonly ShippingDbContext _dbContext;

    public SupplierServiceTests()
    {
        // ✅ In-Memory DB
        var options = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ShippingDbContext(options);

        // ✅ Mock user service
        _mockUserService = new Mock<IUserService>();
        _mockUserService.Setup(u => u.UserId).Returns("test-user");

        // ✅ Mock configuration for RabbitMQ (only if needed in future)
        var config = new Dictionary<string, string>
{
    { "RabbitMQ:Host", "localhost" },
    { "RabbitMQ:Port", "5672" },
    { "RabbitMQ:Username", "guest" },
    { "RabbitMQ:Password", "guest" }
};
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // ✅ Service under test
        _service = new SupplierService(_dbContext, _configuration, _mockUserService.Object);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldCreateSuccessfully()
    {
        // Arrange
        var dto = new SupplierCreateDto
        {
            SupplierName = "Supplier X"
        };

        // Act
        var response = await _service.CreateSupplierAsync(dto, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().BeTrue();
        //  response.Data.name.Should().Be("Supplier X");
        response.Message.Should().Be("Supplier Created Successfully.");

        var supplierInDb = await _dbContext.Suppliers.FirstOrDefaultAsync();
        supplierInDb.Should().NotBeNull();
        supplierInDb?.SupplierName.Should().Be("Supplier X");
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldHandleDbFailure()
    {
        // Arrange: simulate DB failure
        _dbContext.Dispose(); // make SaveChangesAsync fail

        var dto = new SupplierCreateDto
        {
            SupplierName = "Supplier Z"
        };

        // Act
        var response = await _service.CreateSupplierAsync(dto, CancellationToken.None);

        // Assert
        response.Status.Should().BeFalse();
        response.Message.Should().Contain("An error occurred");
    }

    // 👉 Optionally test ConfirmShippedByIdAsync separately
    [Fact]
    public async Task ConfirmShippedByIdAsync_ShouldReturnTrue_WhenInsertSucceeds()
    {
        var dto = new ShippingDto
        {
            UserId = "test-user",
            OrderId = "123",
            Consumer = "Ali",
            Address = new ShippingAddress
            {
                HomeAddress = "Street 1",
                City = "Lahore",
                Region = "Punjab",
                Country = "PK",
                Phone = "12345678"
            }
        };

        var result = await _service.ConfirmShippedByIdAsync(dto, 1);

        result.Should().BeTrue();
    }

    // Delete OrderId Test
    public async Task DeleteOrderId_ShouldReturn_Success()
    {

    }
}
