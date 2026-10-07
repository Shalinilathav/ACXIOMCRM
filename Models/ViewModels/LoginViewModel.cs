using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models.ViewModels;
public class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email")] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Display(Name = "Remember Me")] public bool RememberMe { get; set; }
}
