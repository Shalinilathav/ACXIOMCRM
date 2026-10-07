using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models.ViewModels;
public class CreateUserViewModel
{
    [Required, MaxLength(100), Display(Name = "Full Name")] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare("Password"), Display(Name = "Confirm Password")] public string ConfirmPassword { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = "SalesExecutive";
    public bool IsActive { get; set; } = true;
}
