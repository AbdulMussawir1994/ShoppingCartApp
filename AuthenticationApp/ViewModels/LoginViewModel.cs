using System.ComponentModel.DataAnnotations;

namespace AuthenticationApp.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "CNIC is required.")]
    [RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be a 13-digit numeric value.")]
    public string CNIC { get; set; } = "4210148778829";

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}
