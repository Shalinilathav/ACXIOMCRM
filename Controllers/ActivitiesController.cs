using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ActivitiesController(ApplicationDbContext context, IAuditService auditService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? activityType, string? status, DateTime? from, DateTime? to)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var query = _context.Activities.Include(a => a.Customer).Include(a => a.Lead).AsQueryable();
        if (isSalesExec) query = query.Where(a => a.AssignedTo == user.Id);
        if (!string.IsNullOrEmpty(activityType)) query = query.Where(a => a.ActivityType == activityType);
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        if (from.HasValue) query = query.Where(a => a.ActivityDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.ActivityDate <= to.Value.AddDays(1));

        ViewData["ActivityType"] = activityType; ViewData["Status"] = status;
        ViewData["From"] = from?.ToString("yyyy-MM-dd"); ViewData["To"] = to?.ToString("yyyy-MM-dd");
        return View(await query.OrderByDescending(a => a.ActivityDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var activity = await _context.Activities
            .Include(a => a.Customer).Include(a => a.Lead)
            .FirstOrDefaultAsync(a => a.ActivityId == id);
        if (activity == null) return NotFound();
        if (isSalesExec && activity.AssignedTo != user.Id) return Forbid();
        return View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new Activity { ActivityDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

        model.CreatedBy = user.Id;
        model.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? user.Id : model.AssignedTo;
        model.CreatedDate = DateTime.UtcNow;

        _context.Activities.Add(model);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Create", "Activity", model.ActivityId.ToString(),
            null, $"{model.ActivityType}: {model.Subject}");
        TempData["SuccessMessage"] = "Activity created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return NotFound();
        if (isSalesExec && activity.AssignedTo != user.Id) return Forbid();

        await PopulateDropdowns(activity.CustomerId, activity.LeadId, activity.AssignedTo);
        return View(activity);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity model)
    {
        if (id != model.ActivityId) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var existing = await _context.Activities.FindAsync(id);
        if (existing == null) return NotFound();
        if (isSalesExec && existing.AssignedTo != user.Id) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(existing.CustomerId, existing.LeadId, existing.AssignedTo);
            return View(model);
        }

        var oldValue = $"{existing.ActivityType}:{existing.Subject}|{existing.Status}";
        existing.ActivityType = model.ActivityType;
        existing.Subject = model.Subject;
        existing.Description = model.Description;
        existing.ActivityDate = model.ActivityDate;
        existing.Status = model.Status;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        if (!isSalesExec) existing.AssignedTo = model.AssignedTo;

        await _context.SaveChangesAsync();
        var newValue = $"{existing.ActivityType}:{existing.Subject}|{existing.Status}";
        await _auditService.LogAsync("Update", "Activity", id.ToString(), oldValue, newValue);
        TempData["SuccessMessage"] = "Activity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities.Include(a => a.Customer).Include(a => a.Lead)
            .FirstOrDefaultAsync(a => a.ActivityId == id);
        if (activity == null) return NotFound();
        return View(activity);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return NotFound();
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Delete", "Activity", id.ToString(), $"{activity.Subject}", null);
        TempData["SuccessMessage"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();

        ViewData["CustomerId"] = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewData["LeadId"] = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewData["AssignedToList"] = new SelectList(users, "Id", "FullName", assignedTo);
        ViewData["ActivityTypes"] = new SelectList(new[] { "Call", "Email", "Meeting", "Task" });
        ViewData["Statuses"] = new SelectList(new[] { "Planned", "Completed", "Cancelled" });
    }
}
