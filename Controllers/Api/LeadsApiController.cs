using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "New", "Contacted", "Qualified", "Unqualified", "Nurturing", "Converted", "Lost"
    };

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low", "Medium", "High", "Critical"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICodeGeneratorService _codeGen;
    private readonly IAuditService _audit;

    public LeadsApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICodeGeneratorService codeGen,
        IAuditService audit)
    {
        _context = context;
        _userManager = userManager;
        _codeGen = codeGen;
        _audit = audit;
    }

    // GET /api/leads?search=...
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        IQueryable<Lead> query = _context.Leads;

        if (!isAdminOrManager)
            query = query.Where(l => l.AssignedTo == currentUser.UserName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(s) ||
                l.Email.ToLower().Contains(s) ||
                l.Phone.Contains(s) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)) ||
                l.LeadCode.ToLower().Contains(s) ||
                l.Status.ToLower().Contains(s));
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadDto
            {
                LeadId        = l.LeadId,
                LeadCode      = l.LeadCode,
                LeadName      = l.LeadName,
                Email         = l.Email,
                Phone         = l.Phone,
                CompanyName   = l.CompanyName,
                Source        = l.Source,
                Status        = l.Status,
                Priority      = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedTo    = l.AssignedTo,
                IsConverted   = l.IsConverted,
                CreatedDate   = l.CreatedDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<LeadDto>>.Ok(leads, $"{leads.Count} lead(s) found."));
    }

    // GET /api/leads/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return NotFound(ApiResponse<object>.Fail($"Lead with ID {id} not found."));

        if (!isAdminOrManager && lead.AssignedTo != currentUser.UserName)
            return Forbid();

        var dto = new LeadDto
        {
            LeadId        = lead.LeadId,
            LeadCode      = lead.LeadCode,
            LeadName      = lead.LeadName,
            Email         = lead.Email,
            Phone         = lead.Phone,
            CompanyName   = lead.CompanyName,
            Source        = lead.Source,
            Status        = lead.Status,
            Priority      = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo    = lead.AssignedTo,
            IsConverted   = lead.IsConverted,
            CreatedDate   = lead.CreatedDate
        };

        return Ok(ApiResponse<LeadDto>.Ok(dto));
    }

    // POST /api/leads
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        // Validate Status value
        if (!AllowedStatuses.Contains(dto.Status))
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", AllowedStatuses)}."));
        }

        // Validate Priority value
        if (!AllowedPriorities.Contains(dto.Priority))
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Invalid priority '{dto.Priority}'. Allowed values: {string.Join(", ", AllowedPriorities)}."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        // Check email uniqueness
        var emailExists = await _context.Leads
            .AnyAsync(l => l.Email.ToLower() == dto.Email.ToLower());
        if (emailExists)
            return Conflict(ApiResponse<object>.Fail($"A lead with email '{dto.Email}' already exists."));

        // Check phone uniqueness
        var phoneExists = await _context.Leads
            .AnyAsync(l => l.Phone == dto.Phone);
        if (phoneExists)
            return Conflict(ApiResponse<object>.Fail($"A lead with phone '{dto.Phone}' already exists."));

        var code = await _codeGen.GenerateLeadCodeAsync();

        var lead = new Lead
        {
            LeadCode      = code,
            LeadName      = dto.LeadName,
            Email         = dto.Email,
            Phone         = dto.Phone,
            CompanyName   = dto.CompanyName,
            Source        = dto.Source,
            Status        = dto.Status,
            Priority      = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            Notes         = dto.Notes,
            AssignedTo    = dto.AssignedTo ?? currentUser.UserName,
            IsConverted   = false,
            CreatedDate   = DateTime.UtcNow,
            CreatedBy = currentUser.Id
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("Create", "Lead", lead.LeadId.ToString(), null, $"Created lead '{lead.LeadName}' ({lead.LeadCode}).");

        var resultDto = new LeadDto
        {
            LeadId        = lead.LeadId,
            LeadCode      = lead.LeadCode,
            LeadName      = lead.LeadName,
            Email         = lead.Email,
            Phone         = lead.Phone,
            CompanyName   = lead.CompanyName,
            Source        = lead.Source,
            Status        = lead.Status,
            Priority      = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo    = lead.AssignedTo,
            IsConverted   = lead.IsConverted,
            CreatedDate   = lead.CreatedDate
        };

        return CreatedAtAction(nameof(GetById), new { id = lead.LeadId },
            ApiResponse<LeadDto>.Ok(resultDto, "Lead created successfully."));
    }

    // PUT /api/leads/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        if (!AllowedStatuses.Contains(dto.Status))
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", AllowedStatuses)}."));
        }

        if (!AllowedPriorities.Contains(dto.Priority))
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Invalid priority '{dto.Priority}'. Allowed values: {string.Join(", ", AllowedPriorities)}."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return NotFound(ApiResponse<object>.Fail($"Lead with ID {id} not found."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");
        if (!isAdminOrManager && lead.AssignedTo != currentUser.UserName)
            return Forbid();

        // Check email uniqueness, excluding self
        var emailExists = await _context.Leads
            .AnyAsync(l => l.Email.ToLower() == dto.Email.ToLower() && l.LeadId != id);
        if (emailExists)
            return Conflict(ApiResponse<object>.Fail($"Another lead with email '{dto.Email}' already exists."));

        // Check phone uniqueness, excluding self
        var phoneExists = await _context.Leads
            .AnyAsync(l => l.Phone == dto.Phone && l.LeadId != id);
        if (phoneExists)
            return Conflict(ApiResponse<object>.Fail($"Another lead with phone '{dto.Phone}' already exists."));

        var oldName = lead.LeadName;
        lead.LeadName      = dto.LeadName;
        lead.Email         = dto.Email;
        lead.Phone         = dto.Phone;
        lead.CompanyName   = dto.CompanyName;
        lead.Source        = dto.Source;
        lead.Status        = dto.Status;
        lead.Priority      = dto.Priority;
        lead.ExpectedValue = dto.ExpectedValue;
        lead.Notes         = dto.Notes;
        lead.AssignedTo    = dto.AssignedTo ?? lead.AssignedTo;
await _context.SaveChangesAsync();

        await _audit.LogAsync("Update", "Lead", lead.LeadId.ToString(), null, $"Updated lead '{oldName}' → '{lead.LeadName}' ({lead.LeadCode}).");

        var resultDto = new LeadDto
        {
            LeadId        = lead.LeadId,
            LeadCode      = lead.LeadCode,
            LeadName      = lead.LeadName,
            Email         = lead.Email,
            Phone         = lead.Phone,
            CompanyName   = lead.CompanyName,
            Source        = lead.Source,
            Status        = lead.Status,
            Priority      = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo    = lead.AssignedTo,
            IsConverted   = lead.IsConverted,
            CreatedDate   = lead.CreatedDate
        };

        return Ok(ApiResponse<LeadDto>.Ok(resultDto, "Lead updated successfully."));
    }

    // DELETE /api/leads/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return NotFound(ApiResponse<object>.Fail($"Lead with ID {id} not found."));

        lead.Status       = "Lost";
await _context.SaveChangesAsync();

        await _audit.LogAsync("Delete", "Lead", lead.LeadId.ToString(), null, $"Deactivated lead '{lead.LeadName}' ({lead.LeadCode}).");

        return Ok(ApiResponse<object>.Ok(new { id = lead.LeadId }, "Lead deactivated successfully."));
    }
}
