using System.Security.Claims;

namespace SalesOrderApi.Repository.UserContext;

public class UserService : IUserService
{
    private readonly IHttpContextAccessor _context;

    public UserService(IHttpContextAccessor context)
    {
        _context = context;
    }

    public string? UserId => _context.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => _context.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public List<string> Roles => _context.HttpContext?.User?
        .FindAll("role")
        .Select(r => r.Value)
        .ToList() ?? new();

    public string? AccessToken
    {
        get
        {
            var authHeader = _context.HttpContext?.Request?.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader)) return null;

            // Remove 'Bearer ' prefix if it exists
            return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring("Bearer ".Length)
                : authHeader;
        }
    }
}
