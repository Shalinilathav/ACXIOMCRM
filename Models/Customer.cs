using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AcxiomCRM.Models;
public class Customer
{
    [Key] public int CustomerId { get; set; }
    [Required, MaxLength(20)] public string CustomerCode { get; set; } = string.Empty;
    [Required, MaxLength(100), Display(Name = "Customer Name")] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, Phone, MaxLength(15), Display(Name = "Phone")] public string Phone { get; set; } = string.Empty;
    [MaxLength(150), Display(Name = "Company Name")] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    [Required] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }
    public string? AssignedTo { get; set; }
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    [NotMapped] public ApplicationUser? AssignedUser { get; set; }
}
