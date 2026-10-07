using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models;
public class FollowUp
{
    [Key] public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
    [Required, Display(Name = "Follow-Up Date")]
    public DateTime FollowUpDate { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(50), Display(Name = "Follow-Up Type")] public string FollowUpType { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Status { get; set; } = "Planned";
    public string? Remarks { get; set; }
    [Required] public string AssignedTo { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
