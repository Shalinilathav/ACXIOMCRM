using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class LeadDto
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public string? AssignedTo { get; set; }
    public bool IsConverted { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateLeadDto
{
    [Required, MaxLength(100)] public string LeadName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    [Required, MaxLength(50)] public string Source { get; set; } = string.Empty;
    [Required] public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";
    [Range(0, double.MaxValue)] public decimal ExpectedValue { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
}
