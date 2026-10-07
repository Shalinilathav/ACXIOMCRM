using System.ComponentModel.DataAnnotations;
namespace AcxiomCRM.Models;
public class AuditLog
{
    [Key] public int AuditLogId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string EntityName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
