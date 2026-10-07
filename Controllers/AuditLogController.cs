using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogController : Controller
{
    private readonly ApplicationDbContext _context;
    public AuditLogController(ApplicationDbContext context) { _context = context; }

    public async Task<IActionResult> Index(string? user, string? module, string? action, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _context.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(user)) query = query.Where(a => a.UserName!.Contains(user));
        if (!string.IsNullOrEmpty(module)) query = query.Where(a => a.EntityName.Contains(module));
        if (!string.IsNullOrEmpty(action)) query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue) query = query.Where(a => a.CreatedDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedDate <= to.Value.AddDays(1));
        var total = await query.CountAsync();
        const int pageSize = 20;
        var logs = await query.OrderByDescending(a => a.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewData["Total"] = total;
        ViewData["Page"] = page;
        ViewData["PageSize"] = pageSize;
        ViewData["User"] = user;
        ViewData["Module"] = module;
        ViewData["Action"] = action;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        return View(logs);
    }
}
