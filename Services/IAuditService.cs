namespace AcxiomCRM.Services;
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null, string? newValue = null, string? details = null, string? result = null);
    Task LogAuthAsync(string action, string email, string? result = null, string? details = null);
}
