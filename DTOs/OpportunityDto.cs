using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class OpportunityDto
{
    public int OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateOpportunityDto
{
    [Required, MaxLength(150)] public string OpportunityName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    [Required] public string Stage { get; set; } = "Qualification";
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")] public decimal Amount { get; set; }
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")] public int Probability { get; set; }
    [Required] public DateTime ExpectedCloseDate { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
}
