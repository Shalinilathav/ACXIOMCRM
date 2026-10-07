using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models;
public class Opportunity
{
    [Key] public int OpportunityId { get; set; }
    [Required, MaxLength(150), Display(Name = "Opportunity Name")] public string OpportunityName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    [Required, MaxLength(50)] public string Stage { get; set; } = "Qualification";
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }
    [Required, Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Open";
    public string? Source { get; set; }
    public string? Notes { get; set; }
    [Required] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? AssignedTo { get; set; }
    public decimal WeightedAmount => Amount * Probability / 100;
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}
