using AuthenticationApp.Helpers;
using AuthenticationApp.Repository.AuthRepository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShoppingCartApp.Helper;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Configuration & Validation --------------------

var jwtSecret = builder.Configuration["JWTKey:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new Exception("JWTKey:Secret must be at least 32 characters long.");

// -------------------- Services Registration --------------------

// ✅ Add Controllers with JSON settings
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.WriteIndented = true;
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// ✅ MongoDB Configuration
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(nameof(MongoDbSettings)));
builder.Services.AddSingleton<IMongoDbSettings>(sp =>
    sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);

// ✅ Custom Application Services
builder.Services.AddScoped<IUserService, UserService>();

// ✅ CORS Policy (allow all, consider tightening in production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowApi", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ✅ JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var config = builder.Configuration;
    var secret = config["JWTKey:Secret"] ?? throw new InvalidOperationException("JWT secret is missing.");
    var issuer = config["JWTKey:ValidIssuer"];
    var audience = config["JWTKey:ValidAudience"];
    var baseKey = Encoding.UTF8.GetBytes(secret);

    var tokenHandler = new JwtSecurityTokenHandler();

    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = issuer,
        ValidAudience = audience,

        // ✅ Use custom signing key resolution using KMAC
        IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value
                      ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            var roles = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .OrderBy(r => r)
                .ToList();

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email) || roles.Count == 0)
                throw new SecurityTokenException("Token is missing required claims.");

            var derivedKey = KmacSecurity.DeriveKmacKey(userId, roles, email, baseKey);
            return new[] { new SymmetricSecurityKey(derivedKey) };
        }
    };
});

builder.Services.AddAuthorization();

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

// ✅ Enable minimal API docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCaching();


// -------------------- App Pipeline --------------------

var app = builder.Build();

// Swagger in Dev only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowApi");

app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
