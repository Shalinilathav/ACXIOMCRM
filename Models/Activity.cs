using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models;
public class Activity
{
    [Key] public int ActivityId { get; set; }
    [Required, MaxLength(50), Display(Name = "Activity Type")] public string ActivityType { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required, Display(Name = "Activity Date")] public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    [Required] public string AssignedTo { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Status { get; set; } = "Planned";
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
