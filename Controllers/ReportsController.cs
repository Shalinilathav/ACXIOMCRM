using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Reports/CustomerReport
    public async Task<IActionResult> CustomerReport(string? status, string? owner, DateTime? from, DateTime? to)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var query = _context.Customers.AsQueryable();

        // SalesExecutive sees only their own
        if (!isAdmin && !isManager)
        {
            query = query.Where(c => c.AssignedTo == currentUser!.Id);
        }
        else if (!string.IsNullOrEmpty(owner))
        {
            query = query.Where(c => c.AssignedTo == owner);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (from.HasValue)
        {
            query = query.Where(c => c.CreatedDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(c => c.CreatedDate <= to.Value.AddDays(1));
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();

        ViewData["SelectedStatus"] = status;
        ViewData["SelectedOwner"] = owner;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["IsAdminOrManager"] = isAdmin || isManager;
        ViewData["UserList"] = await _userManager.Users.ToListAsync();

        return View(customers);
    }

    // GET: Reports/LeadReport
    public async Task<IActionResult> LeadReport(string? status, string? source, string? owner)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var query = _context.Leads.AsQueryable();

        // SalesExecutive sees only their own
        if (!isAdmin && !isManager)
        {
            query = query.Where(l => l.AssignedTo == currentUser!.Id);
        }
        else if (!string.IsNullOrEmpty(owner))
        {
            query = query.Where(l => l.AssignedTo == owner);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrEmpty(source))
        {
            query = query.Where(l => l.Source == source);
        }

        var leads = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

        ViewData["SelectedStatus"] = status;
        ViewData["SelectedSource"] = source;
        ViewData["SelectedOwner"] = owner;
        ViewData["IsAdminOrManager"] = isAdmin || isManager;
        ViewData["UserList"] = await _userManager.Users.ToListAsync();

        return View(leads);
    }

    // GET: Reports/OpportunityReport
    public async Task<IActionResult> OpportunityReport(string? stage, string? status, string? owner)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var query = _context.Opportunities
            .Include(o => o.Customer)
            .AsQueryable();

        // SalesExecutive sees only their own
        if (!isAdmin && !isManager)
        {
            query = query.Where(o => o.AssignedTo == currentUser!.Id);
        }
        else if (!string.IsNullOrEmpty(owner))
        {
            query = query.Where(o => o.AssignedTo == owner);
        }

        if (!string.IsNullOrEmpty(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var opportunities = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();

        ViewData["SelectedStage"] = stage;
        ViewData["SelectedStatus"] = status;
        ViewData["SelectedOwner"] = owner;
        ViewData["IsAdminOrManager"] = isAdmin || isManager;
        ViewData["UserList"] = await _userManager.Users.ToListAsync();
        ViewData["TotalValue"] = opportunities.Sum(o => o.Amount);

        return View(opportunities);
    }

    // GET: Reports/FollowUpReport
    public async Task<IActionResult> FollowUpReport(string? status, DateTime? from, DateTime? to)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .AsQueryable();

        // SalesExecutive sees only their own
        if (!isAdmin && !isManager)
        {
            query = query.Where(f => f.AssignedTo == currentUser!.Id);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (from.HasValue)
        {
            query = query.Where(f => f.FollowUpDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(f => f.FollowUpDate <= to.Value.AddDays(1));
        }

        var followUps = await query.OrderByDescending(f => f.FollowUpDate).ToListAsync();

        ViewData["SelectedStatus"] = status;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");

        return View(followUps);
    }

    // GET: Reports/PipelineReport
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> PipelineReport()
    {
        var opportunities = await _context.Opportunities
            .Include(o => o.Customer)
            .ToListAsync();

        // Group by stage
        var byStage = opportunities
            .GroupBy(o => o.Stage)
            .Select(g => new
            {
                Stage = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(o => o.Amount)
            })
            .OrderBy(g => g.Stage)
            .ToList();

        // Group by owner
        var users = await _userManager.Users.ToListAsync();
        var byOwner = opportunities
            .GroupBy(o => o.AssignedTo)
            .Select(g => new
            {
                OwnerId = g.Key,
                OwnerName = users.FirstOrDefault(u => u.Id == g.Key)?.FullName ?? "Unknown",
                Count = g.Count(),
                TotalAmount = g.Sum(o => o.Amount)
            })
            .OrderByDescending(g => g.TotalAmount)
            .ToList();

        ViewData["ByStage"] = byStage;
        ViewData["ByOwner"] = byOwner;
        ViewData["GrandTotal"] = opportunities.Sum(o => o.Amount);
        ViewData["TotalCount"] = opportunities.Count;

        return View(opportunities);
    }

    // GET: Reports/AuditReport
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AuditReport(string? action, string? user, DateTime? from, DateTime? to)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(a => a.Action.Contains(action));
        }

        if (!string.IsNullOrEmpty(user))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(user));
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= to.Value.AddDays(1));
        }

        var logs = await query.OrderByDescending(a => a.CreatedDate).Take(500).ToListAsync();

        ViewData["SelectedAction"] = action;
        ViewData["SelectedUser"] = user;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");

        return View(logs);
    }

    // GET: Reports/UserActivityReport
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UserActivityReport()
    {
        var users = await _userManager.Users.ToListAsync();

        var auditLogs = await _context.AuditLogs
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                ActionCount = g.Count(),
                LastActivity = g.Max(a => a.CreatedDate)
            })
            .ToListAsync();

        var report = users.Select(u => new
        {
            UserId = u.Id,
            FullName = u.FullName ?? u.UserName ?? "Unknown",
            Email = u.Email ?? string.Empty,
            ActionCount = auditLogs.FirstOrDefault(a => a.UserId == u.Id)?.ActionCount ?? 0,
            LastActivity = auditLogs.FirstOrDefault(a => a.UserId == u.Id)?.LastActivity
        })
        .OrderByDescending(r => r.ActionCount)
        .ToList();

        ViewData["Report"] = report;
        return View();
    }
}
