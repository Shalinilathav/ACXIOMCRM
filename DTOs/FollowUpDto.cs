using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.DTOs;

public class CreateFollowUpDto
{
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(50)]  public string FollowUpType { get; set; } = string.Empty;
    [Required]                 public DateTime FollowUpDate { get; set; }
    public string? Remarks { get; set; }
    public string? AssignedTo { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
}
