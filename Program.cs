using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Identity ────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy
    options.Password.RequiredLength            = 8;
    options.Password.RequireDigit             = true;
    options.Password.RequireLowercase         = true;
    options.Password.RequireUppercase         = true;
    options.Password.RequireNonAlphanumeric   = false;

    // Lockout
    options.Lockout.MaxFailedAccessAttempts   = 5;
    options.Lockout.DefaultLockoutTimeSpan    = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers        = true;

    // User
    options.User.RequireUniqueEmail           = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ─── Cookie ──────────────────────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath           = "/Account/Login";
    options.AccessDeniedPath    = "/Account/AccessDenied";
    options.ExpireTimeSpan      = TimeSpan.FromHours(8);
    options.SlidingExpiration   = true;
});

// ─── Application Services ────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService,          AuditService>();
builder.Services.AddScoped<IDashboardService,      DashboardService>();
builder.Services.AddScoped<ICodeGeneratorService,  CodeGeneratorService>();

// ─── MVC ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();

var app = builder.Build();

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// ─── Database Migration & Seeding ────────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(async () =>
{
    await using var scope = app.Services.CreateAsyncScope();
    var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger      = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Apply any pending migrations
        await db.Database.MigrateAsync();

        // Ensure roles exist
        string[] roles = ["Admin", "Manager", "SalesExecutive"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed users
        await SeedUserAsync(userManager, logger,
            email:    "admin@acxiomcrm.com",
            fullName: "System Administrator",
            password: "Admin@1234",
            role:     "Admin");

        await SeedUserAsync(userManager, logger,
            email:    "manager@acxiomcrm.com",
            fullName: "Default Manager",
            password: "Manager@1234",
            role:     "Manager");

        await SeedUserAsync(userManager, logger,
            email:    "sales@acxiomcrm.com",
            fullName: "Default Sales Executive",
            password: "Sales@1234",
            role:     "SalesExecutive");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
});

app.Run();

// ─── Local helper ────────────────────────────────────────────────────────────
static async Task SeedUserAsync(
    UserManager<ApplicationUser> userManager,
    ILogger logger,
    string email,
    string fullName,
    string password,
    string role)
{
    if (await userManager.FindByEmailAsync(email) != null) return;

    var user = new ApplicationUser
    {
        UserName     = email,
        Email        = email,
        FullName     = fullName,
        IsActive     = true,
        CreatedDate  = DateTime.UtcNow,
        EmailConfirmed = true
    };

    var result = await userManager.CreateAsync(user, password);
    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Seeded user {Email} with role {Role}.", email, role);
    }
    else
    {
        foreach (var err in result.Errors)
            logger.LogWarning("Seed user {Email} error: {Desc}", email, err.Description);
    }
}
