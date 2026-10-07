using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models;
public class Lead
{
    [Key] public int LeadId { get; set; }
    [Required, MaxLength(20)] public string LeadCode { get; set; } = string.Empty;
    [Required, MaxLength(100), Display(Name = "Lead Name")] public string LeadName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, Phone, MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150), Display(Name = "Company Name")] public string? CompanyName { get; set; }
    [Required, MaxLength(50)] public string Source { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Status { get; set; } = "New";
    [Required, MaxLength(20)] public string Priority { get; set; } = "Medium";
    [Range(0, double.MaxValue, ErrorMessage = "Expected value must be a positive number."), Display(Name = "Expected Value")]
    public decimal ExpectedValue { get; set; }
    public string? Notes { get; set; }
    [Required] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? AssignedTo { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public bool IsConverted { get; set; }
    public DateTime? ConvertedDate { get; set; }
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
