using AuthenticationApp.Helpers;
using AuthenticationApp.Models;
using AuthenticationApp.Repository.AuthRepository;
using AuthenticationApp.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;

namespace TestingMicroservices.RepositoryTest.Auth;

public class AuthRepositoryTests : IDisposable
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IAsyncCursor<AppUser>> _mockCursor;
    private readonly Mock<IMongoCollection<AppUser>> _mockCollection;
    private readonly Mock<IMongoDatabase> _mockDatabase;
    private readonly Mock<IMongoClient> _mockClient;
    private readonly Mock<IMongoDbSettings> _mockSettings;
    private readonly UserService _service;

    public AuthRepositoryTests()
    {
        // 1. Configuration values
        var configValues = new Dictionary<string, string>
        {
            ["JWTKey:Secret"] = "TestSecretKey12345678901234567890",
            ["JWTKey:ValidIssuer"] = "TestIssuer",
            ["JWTKey:ValidAudience"] = "TestAudience",
            ["JWTKey:TokenExpiryTimeInMinutes"] = "30"
        };

        _mockConfig = new Mock<IConfiguration>();
        foreach (var kvp in configValues)
        {
            _mockConfig.Setup(c => c[kvp.Key]).Returns(kvp.Value);
        }

        // 2. Setup Mongo mocks
        _mockCursor = new Mock<IAsyncCursor<AppUser>>();
        _mockCollection = new Mock<IMongoCollection<AppUser>>();
        _mockDatabase = new Mock<IMongoDatabase>();
        _mockClient = new Mock<IMongoClient>();
        _mockSettings = new Mock<IMongoDbSettings>();

        _mockSettings.Setup(x => x.ConnectionString).Returns("mongodb://localhost:27017");
        _mockSettings.Setup(x => x.DatabaseName).Returns("TestDb");

        _mockDatabase.Setup(x => x.GetCollection<AppUser>(nameof(AppUser), null)).Returns(_mockCollection.Object);
        _mockClient.Setup(x => x.GetDatabase("TestDb", null)).Returns(_mockDatabase.Object);

        _service = new UserService(_mockSettings.Object, _mockConfig.Object);
    }

    [Fact]
    public async Task RegisterUser_ShouldSucceed_WhenNewUser()
    {
        var model = new RegisterViewModel
        {
            Username = "John",
            Email = "john@example.com",
            CNIC = "4210148778829",
            Password = "123456",
            Role = "User"
        };

        SetupEmptyFind(); // No email or CNIC exists

        var result = await _service.RegisterUser(model, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().BeTrue();
        result.Message.Should().Be("Register Successful");
        result.Data.Identity.Should().Be(model.CNIC);
    }

    [Fact]
    public async Task RegisterUser_ShouldFail_WhenEmailExists()
    {
        var model = new RegisterViewModel { Username = "John", Email = "john@example.com", CNIC = "12345", Password = "pass", Role = "User" };
        SetupFindWithUsers(new List<AppUser> { new AppUser { Email = model.Email } });

        var result = await _service.RegisterUser(model, CancellationToken.None);

        result.Status.Should().BeFalse();
        result.Message.Should().Be("CNIC is already registered.");
    }

    [Fact]
    public async Task RegisterUser_ShouldFail_WhenCNICExists()
    {
        var model = new RegisterViewModel { Username = "John", Email = "new@example.com", CNIC = "12345", Password = "pass", Role = "User" };
        SetupFindWithUsers(new List<AppUser> { new AppUser { CNIC = model.CNIC } });

        var result = await _service.RegisterUser(model, CancellationToken.None);

        result.Status.Should().BeFalse();
        result.Message.Should().Be("CNIC is already registered.");
    }

    [Fact]
    public async Task LoginUser_ShouldFail_WhenUserNotFound()
    {
        SetupFindWithUsers(new List<AppUser>()); // No user

        var result = await _service.LoginUser(new LoginViewModel { CNIC = "wrong", Password = "x" }, CancellationToken.None);

        result.Status.Should().BeFalse();
        result.Message.Should().Be("CNIC is invalid.");
    }

    [Fact]
    public async Task LoginUser_ShouldFail_WhenPasswordIsInvalid()
    {
        var user = new AppUser
        {
            CNIC = "12345",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct")
        };
        SetupFindWithUsers(new List<AppUser> { user });

        var result = await _service.LoginUser(new LoginViewModel { CNIC = "12345", Password = "wrong" }, CancellationToken.None);

        result.Status.Should().BeFalse();
        result.Message.Should().Be("Password is invalid.");
    }

    [Fact]
    public async Task LoginUser_ShouldSucceed_WhenCredentialsAreCorrect()
    {
        var user = new AppUser
        {
            Id = "user123",
            CNIC = "12345",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mypassword"),
            Roles = new List<string> { "Admin" }
        };
        SetupFindWithUsers(new List<AppUser> { user });

        var result = await _service.LoginUser(new LoginViewModel { CNIC = "12345", Password = "mypassword" }, CancellationToken.None);

        result.Status.Should().BeTrue();
        result.Message.Should().Be("Login Successful");
        result?.Data?.AccessToken.Should().NotBeNullOrEmpty();
    }

    // ---------- HELPERS ----------

    private void SetupEmptyFind()
    {
        _mockCursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(false);
        _mockCollection.Setup(x => x.FindAsync(It.IsAny<FilterDefinition<AppUser>>(), It.IsAny<FindOptions<AppUser, AppUser>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);
    }

    private void SetupFindWithUsers(List<AppUser> users)
    {
        _mockCursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        _mockCursor.SetupGet(x => x.Current).Returns(users);

        _mockCollection.Setup(x => x.FindAsync(It.IsAny<FilterDefinition<AppUser>>(), It.IsAny<FindOptions<AppUser, AppUser>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockCursor.Object);
    }

    public void Dispose()
    {
        // Nothing to dispose in this mock-based test, but interface required
    }
}
