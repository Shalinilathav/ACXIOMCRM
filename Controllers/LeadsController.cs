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
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICodeGeneratorService _codeGenerator;

    private static readonly string[] ValidStatuses =
        ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];

    private static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["New"] = ["Contacted", "Qualified", "Unqualified", "Lost"],
        ["Contacted"] = ["Qualified", "Unqualified", "Lost"],
        ["Qualified"] = ["Converted", "Lost"],
        ["Unqualified"] = [],
        ["Converted"] = [],
        ["Lost"] = []
    };

    public LeadsController(ApplicationDbContext context, IAuditService auditService,
        UserManager<ApplicationUser> userManager, ICodeGeneratorService codeGenerator)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
        _codeGenerator = codeGenerator;
    }

    public async Task<IActionResult> Index(string? leadName, string? company, string? status, string? priority)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var query = _context.Leads.AsQueryable();
        if (role == "SalesExecutive") query = query.Where(l => l.AssignedTo == user.Id);

        if (!string.IsNullOrWhiteSpace(leadName))
            query = query.Where(l => l.LeadName.Contains(leadName));
        if (!string.IsNullOrWhiteSpace(company))
            query = query.Where(l => l.CompanyName != null && l.CompanyName.Contains(company));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(l => l.Priority == priority);

        ViewData["LeadName"] = leadName; ViewData["Company"] = company;
        ViewData["Status"] = status; ViewData["Priority"] = priority;
        return View(await query.OrderByDescending(l => l.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var lead = await _context.Leads
            .Include(l => l.FollowUps).Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return NotFound();
        if (role == "SalesExecutive" && lead.AssignedTo != user.Id) return Forbid();
        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new Lead());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (!ValidStatuses.Contains(model.Status))
            ModelState.AddModelError("Status", "Invalid status selected.");

        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

        model.LeadCode = await _codeGenerator.GenerateLeadCodeAsync();
        model.CreatedBy = user.Id;
        model.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? user.Id : model.AssignedTo;
        model.CreatedDate = DateTime.UtcNow;

        _context.Leads.Add(model);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Create", "Lead", model.LeadId.ToString(),
            null, $"{model.LeadCode} - {model.LeadName}");
        TempData["SuccessMessage"] = $"Lead {model.LeadCode} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        if (role == "SalesExecutive" && lead.AssignedTo != user.Id) return Forbid();

        await PopulateDropdowns(lead.AssignedTo);
        return View(lead);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead model)
    {
        if (id != model.LeadId) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var existing = await _context.Leads.FindAsync(id);
        if (existing == null) return NotFound();
        if (role == "SalesExecutive" && existing.AssignedTo != user.Id) return Forbid();

        // Status transition validation
        if (existing.Status != model.Status)
        {
            if (!AllowedTransitions.TryGetValue(existing.Status, out var allowed) ||
                !allowed.Contains(model.Status))
            {
                ModelState.AddModelError("Status",
                    $"Cannot transition from '{existing.Status}' to '{model.Status}'.");
            }
        }

        if (!ModelState.IsValid) { await PopulateDropdowns(existing.AssignedTo); return View(model); }

        var oldValue = $"Status:{existing.Status}|Name:{existing.LeadName}";
        existing.LeadName = model.LeadName;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName;
        existing.Source = model.Source;
        existing.Status = model.Status;
        existing.Priority = model.Priority;
        existing.ExpectedValue = model.ExpectedValue;
        existing.Notes = model.Notes;
        if (role != "SalesExecutive") existing.AssignedTo = model.AssignedTo;

        await _context.SaveChangesAsync();
        var newValue = $"Status:{existing.Status}|Name:{existing.LeadName}";
        await _auditService.LogAsync("Update", "Lead", id.ToString(), oldValue, newValue);
        TempData["SuccessMessage"] = "Lead updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertLead(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        if (role == "SalesExecutive" && lead.AssignedTo != user.Id) return Forbid();
        if (lead.Status != "Qualified")
        {
            TempData["ErrorMessage"] = "Only Qualified leads can be converted.";
            return RedirectToAction(nameof(Index));
        }

        lead.IsConverted = true;
        lead.ConvertedDate = DateTime.UtcNow;
        lead.Status = "Converted";
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Convert", "Lead", id.ToString(),
            "Qualified", "Converted", $"Lead {lead.LeadCode} converted");
        TempData["SuccessMessage"] = $"Lead {lead.LeadCode} has been converted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        return View(lead);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Delete", "Lead", id.ToString(),
            $"{lead.LeadCode} - {lead.LeadName}", null);
        TempData["SuccessMessage"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(string? selectedUserId = null)
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewData["AssignedToList"] = users.Select(u => new SelectListItem
        {
            Value = u.Id, Text = $"{u.FullName} ({u.Email})", Selected = u.Id == selectedUserId
        }).ToList();
    }
}
