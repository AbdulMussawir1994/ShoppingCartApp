namespace ShippingOrderApi.Repository.UserContext;

public interface IUserService
{
    string? UserId { get; }
    string? Email { get; }
    List<string> Roles { get; }
    string? AccessToken { get; }
}
