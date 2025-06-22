using AuthenticationApp.Utilities;
using AuthenticationApp.ViewModels;

namespace AuthenticationApp.Repository.AuthRepository;

public interface IUserService
{
    Task<MobileResponse<LoginResponseModel>> LoginUser(LoginViewModel model, CancellationToken cancellationToken);
    Task<MobileResponse<RegisterViewResponse>> RegisterUser(RegisterViewModel model, CancellationToken cancellationToken);
}
