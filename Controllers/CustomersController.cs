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
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICodeGeneratorService _codeGenerator;

    public CustomersController(ApplicationDbContext context, IAuditService auditService,
        UserManager<ApplicationUser> userManager, ICodeGeneratorService codeGenerator)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
        _codeGenerator = codeGenerator;
    }

    public async Task<IActionResult> Index(string? name, string? email, string? phone, string? company, string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var query = _context.Customers.AsQueryable();

        if (role == "SalesExecutive")
            query = query.Where(c => c.AssignedTo == user.Id);

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(c => c.CustomerName.Contains(name));
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(c => c.Email.Contains(email));
        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(c => c.Phone.Contains(phone));
        if (!string.IsNullOrWhiteSpace(company))
            query = query.Where(c => c.CompanyName != null && c.CompanyName.Contains(company));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        ViewData["Name"] = name; ViewData["Email"] = email;
        ViewData["Phone"] = phone; ViewData["Company"] = company; ViewData["Status"] = status;
        ViewData["Role"] = role;
        return View(await query.OrderByDescending(c => c.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var customer = await _context.Customers
            .Include(c => c.Leads).Include(c => c.FollowUps).Include(c => c.Opportunities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return NotFound();

        if (role == "SalesExecutive" && customer.AssignedTo != user.Id)
            return Forbid();

        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateAssignedUsers();
        return View(new Customer());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (!string.IsNullOrWhiteSpace(model.Email) &&
            await _context.Customers.AnyAsync(c => c.Email == model.Email))
            ModelState.AddModelError("Email", "A customer with this email address already exists.");

        if (!string.IsNullOrWhiteSpace(model.Phone) &&
            await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");

        if (!ModelState.IsValid)
        {
            await PopulateAssignedUsers();
            return View(model);
        }

        model.CustomerCode = await _codeGenerator.GenerateCustomerCodeAsync();
        model.CreatedBy = user.Id;
        model.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? user.Id : model.AssignedTo;
        model.CreatedDate = DateTime.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status;

        _context.Customers.Add(model);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Create", "Customer", model.CustomerId.ToString(),
            null, $"{model.CustomerCode} - {model.CustomerName}");
        TempData["SuccessMessage"] = $"Customer {model.CustomerCode} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        if (role == "SalesExecutive" && customer.AssignedTo != user.Id) return Forbid();

        await PopulateAssignedUsers(customer.AssignedTo);
        return View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer model)
    {
        if (id != model.CustomerId) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "SalesExecutive";

        var existing = await _context.Customers.FindAsync(id);
        if (existing == null) return NotFound();
        if (role == "SalesExecutive" && existing.AssignedTo != user.Id) return Forbid();

        if (!string.IsNullOrWhiteSpace(model.Email) &&
            await _context.Customers.AnyAsync(c => c.Email == model.Email && c.CustomerId != id))
            ModelState.AddModelError("Email", "A customer with this email already exists.");

        if (!string.IsNullOrWhiteSpace(model.Phone) &&
            await _context.Customers.AnyAsync(c => c.Phone == model.Phone && c.CustomerId != id))
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");

        if (!ModelState.IsValid)
        {
            await PopulateAssignedUsers(existing.AssignedTo);
            return View(model);
        }

        var oldValue = $"Name:{existing.CustomerName}|Email:{existing.Email}|Status:{existing.Status}";

        existing.CustomerName = model.CustomerName;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName;
        existing.Address = model.Address;
        existing.City = model.City;
        existing.State = model.State;
        existing.Status = model.Status;
        existing.Notes = model.Notes;
        existing.ModifiedDate = DateTime.UtcNow;
        if (role != "SalesExecutive" && !string.IsNullOrEmpty(model.AssignedTo))
            existing.AssignedTo = model.AssignedTo;

        await _context.SaveChangesAsync();
        var newValue = $"Name:{existing.CustomerName}|Email:{existing.Email}|Status:{existing.Status}";
        await _auditService.LogAsync("Update", "Customer", id.ToString(), oldValue, newValue);
        TempData["SuccessMessage"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Delete", "Customer", id.ToString(),
            $"{customer.CustomerCode} - {customer.CustomerName}", null);
        TempData["SuccessMessage"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAssignedUsers(string? selectedId = null)
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewData["AssignedUsers"] = users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = $"{u.FullName} ({u.Email})",
            Selected = u.Id == selectedId
        }).ToList();
    }
}
