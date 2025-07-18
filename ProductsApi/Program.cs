using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProductsApi.DbContextClass;
using ProductsApi.Helpers;
using ProductsApi.Repository.ProductRepository;
using ProductsApi.Repository.RabbitMqProducer;
using ProductsApi.Repository.UserContext;
using ProductsApi.Utilities;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------- Configuration & Validation -----------------------------

var configuration = builder.Configuration;

// ✅ Validate JWT Secret
var jwtSecret = configuration["JWTKey:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new Exception("JWTKey:Secret must be at least 32 characters long.");

// ----------------------------- Services Registration -----------------------------

// ✅ Database Context with SQL Server and Retry Logic
builder.Services.AddDbContextPool<ProductDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout((int)TimeSpan.FromMinutes(1).TotalSeconds)
    )
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
);

// 🔹 Redis Caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetSection("RedisConnection").GetValue<string>("LocalHost");
    options.InstanceName = configuration.GetSection("RedisConnection").GetValue<string>("InstanceName");
});

// ✅ JSON Serialization Config (Performance + Compatibility)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// ✅ CORS Policy (open for development, restrict in production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowApi", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
    );
});

// ✅ Register Application Services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IUserService, UserService>();

var machineId = builder.Configuration.GetValue<int>("Snowflake:MachineId");
builder.Services.AddSingleton(new SnowflakeIdGenerator(machineId));

// ✅ Add Services for RabbitMQ Producer
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

// ✅ Mapster Config for DTO Mapping
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(new MapsterProfile());

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// JWT Authentication & Authorization
builder.AddAppAuthentication();
builder.Services.AddAuthorization();

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

// ✅ Swagger Configuration with JWT Support
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
            Title = $"Product API - {description.GroupName.ToUpperInvariant()}",
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

// ✅ Optional Response Caching for Improved Performance
builder.Services.AddResponseCaching();

// ----------------------------- Application Pipeline -----------------------------

var app = builder.Build();

//using (var scope = app.Services.CreateScope())
//{
//    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

//    // If using migrations:
//    // await dbContext.Database.MigrateAsync();

//    // Optional safety check
//    if (!dbContext.Products.Any())
//    {
//        await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('OrderDetails', RESEED, 999)");
//    }
//}

app.ConfigureSwagger();

app.UseHttpsRedirection();

app.UseCors("AllowApi");

app.UseResponseCaching();

app.UseRouting();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
