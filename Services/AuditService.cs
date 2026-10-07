using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null, string? newValue = null, string? details = null, string? result = null)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var userId = ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = ctx?.User?.FindFirstValue(ClaimTypes.Email) ?? ctx?.User?.Identity?.Name;
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();

        var log = new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            Details = details,
            Result = result ?? "Success",
            IpAddress = ip,
            CreatedDate = DateTime.UtcNow
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task LogAuthAsync(string action, string email, string? result = null, string? details = null)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var log = new AuditLog
        {
            UserName = email,
            Action = action,
            EntityName = "Authentication",
            Result = result ?? "Success",
            Details = details,
            IpAddress = ip,
            CreatedDate = DateTime.UtcNow
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
