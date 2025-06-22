using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProductsApi.DbContextClass;
using ProductsApi.Helpers;
using ProductsApi.Repository.ProductRepository;
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
//builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));
//builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

// ✅ Mapster Config for DTO Mapping
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(new MapsterProfile());

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// JWT Authentication & Authorization
builder.AddAppAuthentication();
builder.Services.AddAuthorization();

// ✅ Swagger Configuration with JWT Support
builder.Services.AddEndpointsApiExplorer();

// 🧭 Swagger Configuration with JWT Bearer
builder.Services.AddSwaggerGen(options =>
{
    // JWT Bearer auth setup
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token below with **Bearer** prefix.\r\nExample: Bearer eyJhbGciOi...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http, // Use Http instead of ApiKey for better support
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

    // Optional: Add XML comment support for controller documentation
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowApi");

app.UseResponseCaching();

app.UseRouting();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
