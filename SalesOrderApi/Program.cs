using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SalesOrderApi.DbContextClass;
using SalesOrderApi.Helpers;
using SalesOrderApi.Repository.OrderRepository;
using SalesOrderApi.Repository.RabbitMqProducer;
using SalesOrderApi.Repository.UserContext;
using SalesOrderApi.Utilities;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------- Configuration & Validation -----------------------------
var configuration = builder.Configuration;

// Validate JWT Secret
var jwtSecret = builder.Configuration["JWTKey:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new Exception("JWTKey:Secret must be at least 32 characters long.");

// ----------------------------- Services Registration -----------------------------

// ✅ Database Context with MySQL and Retry Logic
builder.Services.AddDbContextPool<OrderDbContext>(options =>
    options.UseMySql(
        configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 34)), // adjust as per your MySQL version
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
            sqlOptions.CommandTimeout(30); // seconds
        })
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
);

// Controllers with consistent, fast JSON serialization config
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// CORS Policy (you can restrict origin in production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowApi", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

//🔄 API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(2, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader(); // Important!
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ✅ Register Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// ✅ Add Services for RabbitMQ Producer
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

// ✅ Mapster Config for DTO Mapping
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(new MapsterProfile());

//builder.Services.AddHttpClient(); // Required for IHttpClientFactory
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient("ProductApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7124/api/v2/Product/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigureHttpClient((sp, client) =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
    var token = httpContext?.Request?.Headers["Authorization"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(token))
    {
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token.Substring(7);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
});

// JWT Authentication & Authorization
builder.AddAppAuthentication();
builder.Services.AddAuthorization();


builder.Services.AddEndpointsApiExplorer();

// 🧭 Swagger Configuration with JWT Bearer
builder.Services.AddSwaggerGen(options =>
{
    var provider = builder.Services.BuildServiceProvider()
                      .GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerDoc(description.GroupName, new OpenApiInfo
        {
            Title = $"Sale Order API - {description.GroupName.ToUpperInvariant()}",
            Version = description.GroupName
        });
    }

    // 🔐 JWT setup (already good in your code)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token below with **Bearer** prefix.\r\nExample: Bearer eyJhbGciOi...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // XML comments (optional)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add Response Caching (optional)
builder.Services.AddResponseCaching();

// ----------------------------- App Pipeline -----------------------------

var app = builder.Build();

// ✅ Set OrderId seed to start from 1000 for MySQL
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

    // Optional: Apply migrations if using EF migrations
    // await dbContext.Database.MigrateAsync();

    // ✅ Only reseed if table is empty
    if (!dbContext.Orders.Any())
    {
        await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE `Order` AUTO_INCREMENT = 1000;");
    }
}

// Swagger for development only
app.ConfigureSwagger();

app.UseHttpsRedirection();

app.UseCors("AllowApi");

app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
