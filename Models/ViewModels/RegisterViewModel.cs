using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models.ViewModels;
public class RegisterViewModel
{
    [Required, Display(Name = "Full Name"), MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm Password")] public string ConfirmPassword { get; set; } = string.Empty;
    [Required, Display(Name = "Role")] public string Role { get; set; } = "SalesExecutive";
}
