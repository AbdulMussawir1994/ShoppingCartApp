using AuthenticationApp.Helpers;
using AuthenticationApp.Models;
using AuthenticationApp.Repository.AuthRepository;
using AuthenticationApp.ViewModels;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;

namespace TestingMicroservices.RepositoryTest.Auth;

public class AuthRepositoryTests
{
    private readonly Mock<IMongoCollection<AppUser>> _mockCollection;
    private readonly Mock<IAsyncCursor<AppUser>> _mockCursor;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IMongoDbSettings> _mockSettings;
    private readonly UserService _service;

    public AuthRepositoryTests()
    {
        _mockCollection = new Mock<IMongoCollection<AppUser>>();
        _mockCursor = new Mock<IAsyncCursor<AppUser>>();
        _mockConfig = new Mock<IConfiguration>();
        _mockSettings = new Mock<IMongoDbSettings>();

        _mockSettings.SetupGet(x => x.ConnectionString).Returns("mongodb://localhost:27017");
        _mockSettings.SetupGet(x => x.DatabaseName).Returns("TestDb");

        // Replace MongoClient with mock using reflection or integration testing. 
        // Here, test the methods with mocked collection only.

        var configValues = new Dictionary<string, string>
        {
            {"JWTKey:Secret", "SuperSecretKey1234567890!"},
            {"JWTKey:ValidIssuer", "TestIssuer"},
            {"JWTKey:ValidAudience", "TestAudience"},
            {"JWTKey:TokenExpiryTimeInMinutes", "60"}
        };

        _mockConfig.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => configValues.ContainsKey(key) ? configValues[key] : null);

        _service = new UserService(_mockSettings.Object, _mockConfig.Object);
    }

    [Fact]
    public async Task RegisterUser_ShouldReturnError_WhenEmailExists()
    {
        // Arrange
        var model = new RegisterViewModel { Email = "test@example.com", CNIC = "12345", Password = "pass", Role = "User", Username = "John" };

        _mockCursor.SetupSequence(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        _mockCursor.SetupGet(x => x.Current).Returns(new List<AppUser> { new AppUser { Email = "test@example.com" } });

        _mockCollection.Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<AppUser>>(),
                It.IsAny<FindOptions<AppUser, AppUser>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);

        // Act
        var result = await _service.RegisterUser(model, CancellationToken.None);

        // Assert
        Assert.False(result.Status);
        Assert.Equal("Email is already registered.", result.Message);
    }

    [Fact]
    public async Task LoginUser_ShouldReturnError_WhenUserNotFound()
    {
        // Arrange
        var model = new LoginViewModel { CNIC = "4210148778829", Password = "123456" };

        _mockCursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockCollection.Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<AppUser>>(),
                It.IsAny<FindOptions<AppUser, AppUser>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);

        // Act
        var result = await _service.LoginUser(model, CancellationToken.None);

        // Assert
        Assert.False(result.Status);
        Assert.Equal("CNIC is invalid.", result.Message);
    }

    [Fact]
    public async Task LoginUser_ShouldReturnError_WhenPasswordInvalid()
    {
        // Arrange
        var model = new LoginViewModel { CNIC = "12345", Password = "wrongpass" };
        var hashedPass = BCrypt.Net.BCrypt.HashPassword("correctpass");

        var user = new AppUser { CNIC = "12345", PasswordHash = hashedPass, Id = "1", Email = "test@test.com", Roles = new List<string> { "User" } };

        _mockCursor.SetupSequence(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        _mockCursor.SetupGet(x => x.Current).Returns(new List<AppUser> { user });

        _mockCollection.Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<AppUser>>(),
                It.IsAny<FindOptions<AppUser, AppUser>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);

        // Act
        var result = await _service.LoginUser(model, CancellationToken.None);

        // Assert
        Assert.False(result.Status);
        Assert.Equal("Password is invalid.", result.Message);
    }

    [Fact]
    public async Task LoginUser_ShouldReturnToken_WhenCredentialsValid()
    {
        // Arrange
        var model = new LoginViewModel { CNIC = "12345", Password = "mypassword" };
        var user = new AppUser
        {
            Id = "user123",
            CNIC = "12345",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mypassword"),
            Roles = new List<string> { "Admin" }
        };

        _mockCursor.SetupSequence(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        _mockCursor.SetupGet(x => x.Current).Returns(new List<AppUser> { user });

        _mockCollection.Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<AppUser>>(),
                It.IsAny<FindOptions<AppUser, AppUser>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);

        // Act
        var result = await _service.LoginUser(model, CancellationToken.None);

        // Assert
        Assert.True(result.Status);
        Assert.NotNull(result.Data.AccessToken);
        Assert.Equal("Login Successful", result.Message);
    }
}
