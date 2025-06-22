using System.ComponentModel.DataAnnotations;

namespace AuthenticationApp.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 4, ErrorMessage = "Username must be between 4 and 50 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(10, MinimumLength = 4)]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "CNIC is required.")]
    [RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be a 13-digit numeric value.")]
    public string CNIC { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^03\d{9}$", ErrorMessage = "Mobile number must be in valid Pakistani format like 03XXXXXXXXX.")]
    public string MobileNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}
