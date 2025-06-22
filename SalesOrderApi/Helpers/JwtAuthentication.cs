using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SalesOrderApi.Helpers;

public static class JwtAuthentication
{
    public static WebApplicationBuilder AddAppAuthentication(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration.GetSection("JWTKey");

        var secret = config["Secret"];
        var issuer = config["ValidIssuer"];
        var audience = config["ValidAudience"];

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("JWTKey:Secret must be at least 32 characters long.");

        var baseKey = Encoding.UTF8.GetBytes(secret);
        var tokenHandler = new JwtSecurityTokenHandler();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
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

                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    var jwt = tokenHandler.ReadJwtToken(token);

                    var userId = jwt.Claims.FirstOrDefault(c =>
                        c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;

                    var email = jwt.Claims.FirstOrDefault(c =>
                        c.Type == JwtRegisteredClaimNames.Email || c.Type == ClaimTypes.Email)?.Value;

                    var roles = jwt.Claims
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

        return builder;
    }
}
