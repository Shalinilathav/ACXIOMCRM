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
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    private static readonly string[] ValidStages =
        ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];

    public OpportunitiesController(ApplicationDbContext context, IAuditService auditService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? name, string? customer, string? stage, string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var query = _context.Opportunities.Include(o => o.Customer).AsQueryable();
        if (role == "SalesExecutive") query = query.Where(o => o.AssignedTo == user.Id);

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(o => o.OpportunityName.Contains(name));
        if (!string.IsNullOrWhiteSpace(customer))
            query = query.Where(o => o.Customer != null && o.Customer.CustomerName.Contains(customer));
        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        ViewData["Name"] = name; ViewData["Customer"] = customer;
        ViewData["Stage"] = stage; ViewData["Status"] = status;
        return View(await query.OrderByDescending(o => o.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var opp = await _context.Opportunities
            .Include(o => o.Customer).Include(o => o.Lead).Include(o => o.FollowUps)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return NotFound();
        if (role == "SalesExecutive" && opp.AssignedTo != user.Id) return Forbid();
        return View(opp);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new Opportunity { ExpectedCloseDate = DateTime.Today.AddDays(30) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        // Business validations
        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        if (model.Probability < 0 || model.Probability > 100)
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        if (model.ExpectedCloseDate.Date < DateTime.Today &&
            model.Stage != "Won" && model.Stage != "Lost")
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        if (!ValidStages.Contains(model.Stage))
            ModelState.AddModelError("Stage", "Invalid stage selected.");

        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

        model.CreatedBy = user.Id;
        model.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? user.Id : model.AssignedTo;
        model.CreatedDate = DateTime.UtcNow;
        model.Status = model.Stage == "Won" ? "Won" : model.Stage == "Lost" ? "Lost" : "Open";

        _context.Opportunities.Add(model);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Create", "Opportunity", model.OpportunityId.ToString(),
            null, $"{model.OpportunityName} | {model.Stage} | ₹{model.Amount:N0}");
        TempData["SuccessMessage"] = $"Opportunity '{model.OpportunityName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();
        if (role == "SalesExecutive" && opp.AssignedTo != user.Id) return Forbid();

        await PopulateDropdowns(opp.CustomerId, opp.AssignedTo);
        return View(opp);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity model)
    {
        if (id != model.OpportunityId) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var existing = await _context.Opportunities.FindAsync(id);
        if (existing == null) return NotFound();
        if (role == "SalesExecutive" && existing.AssignedTo != user.Id) return Forbid();

        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        if (model.Probability < 0 || model.Probability > 100)
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        if (model.ExpectedCloseDate.Date < DateTime.Today &&
            model.Stage != "Won" && model.Stage != "Lost")
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past for active opportunities.");

        if (!ModelState.IsValid) { await PopulateDropdowns(existing.CustomerId, existing.AssignedTo); return View(model); }

        var oldValue = $"Stage:{existing.Stage}|Amount:{existing.Amount}|CloseDate:{existing.ExpectedCloseDate:d}";
        existing.OpportunityName = model.OpportunityName;
        existing.CustomerId = model.CustomerId;
        existing.Stage = model.Stage;
        existing.Amount = model.Amount;
        existing.Probability = model.Probability;
        existing.ExpectedCloseDate = model.ExpectedCloseDate;
        existing.Source = model.Source;
        existing.Notes = model.Notes;
        existing.Status = model.Stage == "Won" ? "Won" : model.Stage == "Lost" ? "Lost" : "Open";
        if (role != "SalesExecutive") existing.AssignedTo = model.AssignedTo;

        await _context.SaveChangesAsync();
        var newValue = $"Stage:{existing.Stage}|Amount:{existing.Amount}|CloseDate:{existing.ExpectedCloseDate:d}";
        await _auditService.LogAsync("Update", "Opportunity", id.ToString(), oldValue, newValue);
        TempData["SuccessMessage"] = "Opportunity updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _context.Opportunities.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return NotFound();
        return View(opp);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();
        _context.Opportunities.Remove(opp);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Delete", "Opportunity", id.ToString(),
            $"{opp.OpportunityName}", null);
        TempData["SuccessMessage"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? selectedCustomerId = null, string? selectedUserId = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        ViewData["Customers"] = customers.Select(c => new SelectListItem
        {
            Value = c.CustomerId.ToString(), Text = c.CustomerName,
            Selected = c.CustomerId == selectedCustomerId
        }).ToList();

        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewData["AssignedToList"] = users.Select(u => new SelectListItem
        {
            Value = u.Id, Text = $"{u.FullName} ({u.Email})", Selected = u.Id == selectedUserId
        }).ToList();
    }
}
