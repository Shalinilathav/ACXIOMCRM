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
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FollowUpsController(ApplicationDbContext context, IAuditService auditService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? status, DateTime? from, DateTime? to, int? customerId, int? leadId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var query = _context.FollowUps
            .Include(f => f.Customer).Include(f => f.Lead).AsQueryable();

        if (isSalesExec) query = query.Where(f => f.AssignedTo == user.Id);
        if (!string.IsNullOrEmpty(status)) query = query.Where(f => f.Status == status);
        if (from.HasValue) query = query.Where(f => f.FollowUpDate >= from.Value);
        if (to.HasValue) query = query.Where(f => f.FollowUpDate <= to.Value.AddDays(1));
        if (customerId.HasValue) query = query.Where(f => f.CustomerId == customerId.Value);
        if (leadId.HasValue) query = query.Where(f => f.LeadId == leadId.Value);

        ViewData["Status"] = status;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Today"] = DateTime.Today;
        return View(await query.OrderBy(f => f.FollowUpDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var followUp = await _context.FollowUps
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (followUp == null) return NotFound();
        if (isSalesExec && followUp.AssignedTo != user.Id) return Forbid();
        return View(followUp);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        await PopulateDropdowns();
        return View(new FollowUp
        {
            FollowUpDate = DateTime.Today.AddDays(1),
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (model.FollowUpDate.Date < DateTime.Today && model.Status == "Planned")
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today for planned follow-ups.");

        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

        model.CreatedBy = user.Id;
        model.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? user.Id : model.AssignedTo;
        model.CreatedDate = DateTime.UtcNow;

        _context.FollowUps.Add(model);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Create", "FollowUp", model.FollowUpId.ToString(),
            null, $"{model.Subject} | {model.FollowUpDate:d}");
        TempData["SuccessMessage"] = "Follow-up created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();
        if (isSalesExec && followUp.AssignedTo != user.Id) return Forbid();

        await PopulateDropdowns(followUp.CustomerId, followUp.LeadId, followUp.AssignedTo);
        return View(followUp);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp model)
    {
        if (id != model.FollowUpId) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var isSalesExec = User.IsInRole("SalesExecutive");

        var existing = await _context.FollowUps.FindAsync(id);
        if (existing == null) return NotFound();
        if (isSalesExec && existing.AssignedTo != user.Id) return Forbid();

        if (model.FollowUpDate.Date < DateTime.Today && model.Status == "Planned")
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be in the past for planned follow-ups.");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(existing.CustomerId, existing.LeadId, existing.AssignedTo);
            return View(model);
        }

        var oldValue = $"Date:{existing.FollowUpDate:d}|Status:{existing.Status}";
        existing.Subject = model.Subject;
        existing.FollowUpType = model.FollowUpType;
        existing.FollowUpDate = model.FollowUpDate;
        existing.Status = model.Status;
        existing.Remarks = model.Remarks;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        existing.OpportunityId = model.OpportunityId;
        if (!isSalesExec) existing.AssignedTo = model.AssignedTo;

        await _context.SaveChangesAsync();
        var newValue = $"Date:{existing.FollowUpDate:d}|Status:{existing.Status}";
        await _auditService.LogAsync("Update", "FollowUp", id.ToString(), oldValue, newValue);
        TempData["SuccessMessage"] = "Follow-up updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        followUp.Status = "Completed";
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Complete", "FollowUp", id.ToString(),
            "Planned", "Completed", followUp.Subject);
        TempData["SuccessMessage"] = "Follow-up marked as completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps
            .Include(f => f.Customer).Include(f => f.Lead)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (followUp == null) return NotFound();
        return View(followUp);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();
        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Delete", "FollowUp", id.ToString(), followUp.Subject, null);
        TempData["SuccessMessage"] = "Follow-up deleted.";
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
        ViewData["FollowUpTypes"] = new SelectList(new[] { "Call", "Email", "Meeting", "Visit" });
        ViewData["Statuses"] = new SelectList(new[] { "Planned", "Completed", "Missed", "Cancelled" });
    }
}
