namespace AuthenticationApp.ViewModels;

public class LoginResponseModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime ExpireTokenTime { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    //  public string RefreshToken { get; set; } = string.Empty;
}
