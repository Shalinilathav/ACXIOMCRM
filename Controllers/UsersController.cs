using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public UsersController(UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
            users = users.Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                     u.Email!.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var vmList = new List<UserManagementViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            vmList.Add(new UserManagementViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                AccessFailedCount = u.AccessFailedCount,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate
            });
        }
        ViewData["Search"] = search;
        return View(vmList);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser
        {
            FullName = model.FullName, Email = model.Email, UserName = model.Email,
            IsActive = model.IsActive, CreatedDate = DateTime.UtcNow, EmailConfirmed = true
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }
        var role = model.Role is "Admin" or "Manager" or "SalesExecutive" ? model.Role : "SalesExecutive";
        await _userManager.AddToRoleAsync(user, role);
        await _auditService.LogAsync("Create", "User", user.Id, null, $"{model.Email} ({role})");
        TempData["SuccessMessage"] = $"User {model.Email} created with role {role}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        var vm = new CreateUserViewModel
        {
            FullName = user.FullName, Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "SalesExecutive", IsActive = user.IsActive
        };
        ViewData["UserId"] = id;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, CreateUserViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        ModelState.Remove("Password"); ModelState.Remove("ConfirmPassword");
        if (!ModelState.IsValid) { ViewData["UserId"] = id; return View(model); }

        var oldRoles = await _userManager.GetRolesAsync(user);
        var oldRole = oldRoles.FirstOrDefault() ?? "None";
        user.FullName = model.FullName; user.IsActive = model.IsActive;
        await _userManager.UpdateAsync(user);

        if (model.Role != oldRole)
        {
            await _userManager.RemoveFromRolesAsync(user, oldRoles);
            await _userManager.AddToRoleAsync(user, model.Role);
        }
        await _auditService.LogAsync("Update", "User", id, $"{oldRole}|Active:{user.IsActive}",
            $"{model.Role}|Active:{model.IsActive}");
        TempData["SuccessMessage"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _userManager.DeleteAsync(user);
        await _auditService.LogAsync("Delete", "User", id, user.Email, null);
        TempData["SuccessMessage"] = "User deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        if (isLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _auditService.LogAsync("Unlock", "User", id, "Locked", "Unlocked");
            TempData["SuccessMessage"] = "User account unlocked.";
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));
            await _auditService.LogAsync("Lock", "User", id, "Active", "Locked");
            TempData["SuccessMessage"] = "User account locked.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["ErrorMessage"] = "Password must be at least 8 characters.";
            return RedirectToAction(nameof(Index));
        }
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            await _auditService.LogAsync("ResetPassword", "User", id, null, null,
                "Admin reset password (value not logged)");
            TempData["SuccessMessage"] = "Password reset successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
        }
        return RedirectToAction(nameof(Index));
    }
}
