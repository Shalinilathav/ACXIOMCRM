using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IAuditService                  _auditService;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser>   userManager,
        IAuditService                  auditService)
    {
        _signInManager = signInManager;
        _userManager   = userManager;
        _auditService  = auditService;
    }

    // ─── Login ───────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // Verify user exists
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            await _auditService.LogAuthAsync("Login", model.Email, "Failed", "User not found");
            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        // Verify account is active
        if (!user.IsActive)
        {
            await _auditService.LogAuthAsync("Login", model.Email, "Failed", "Account inactive");
            ModelState.AddModelError("", "Your account has been deactivated. Please contact an administrator.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _auditService.LogAuthAsync("Login", model.Email, "Success");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAuthAsync("Login", model.Email, "LockedOut", "Account locked out");
            ModelState.AddModelError("", "Your account has been locked out due to multiple failed login attempts. Please try again later.");
            return View(model);
        }

        await _auditService.LogAuthAsync("Login", model.Email, "Failed", "Invalid credentials");
        ModelState.AddModelError("", "Invalid email or password.");
        return View(model);
    }

    // ─── Register ────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            FullName    = model.FullName,
            Email       = model.Email,
            UserName    = model.Email,
            IsActive    = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var role = model.Role;
            if (role != "Admin" && role != "Manager" && role != "SalesExecutive")
                role = "SalesExecutive";

            await _userManager.AddToRoleAsync(user, role);
            await _auditService.LogAuthAsync("Register", model.Email, "Success", $"Role: {role}");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // ─── Logout ──────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        var email = User.Identity?.Name ?? "";
        await _auditService.LogAuthAsync("Logout", email, "Success");
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ─── Access Denied ───────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
