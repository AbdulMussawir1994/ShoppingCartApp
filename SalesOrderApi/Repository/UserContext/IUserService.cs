namespace SalesOrderApi.Repository.UserContext;

public interface IUserService
{
    string? UserId { get; }
    string? Email { get; }
    List<string> Roles { get; }
}
