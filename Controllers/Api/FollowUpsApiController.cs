using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Route("api/followups"), Authorize]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public FollowUpsApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
    {
        _context = context; _userManager = userManager; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? assignedTo)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        bool isSalesExec = roles.Contains("SalesExecutive");

        var query = _context.FollowUps
            .Include(f => f.Customer).Include(f => f.Lead)
            .AsQueryable();

        if (isSalesExec) query = query.Where(f => f.AssignedTo == user!.Id);
        if (!string.IsNullOrEmpty(status)) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrEmpty(assignedTo) && !isSalesExec) query = query.Where(f => f.AssignedTo == assignedTo);

        var list = await query.OrderByDescending(f => f.FollowUpDate).Select(f => new
        {
            f.FollowUpId, f.Subject, f.FollowUpType, f.FollowUpDate,
            f.Status, f.AssignedTo, f.Remarks, f.CreatedDate,
            CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
            LeadName = f.Lead != null ? f.Lead.LeadName : null
        }).ToListAsync();

        return Ok(new ApiResponse<object> { Success = true, Message = "Success", Data = list });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var f = await _context.FollowUps.Include(x => x.Customer).Include(x => x.Lead).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound(ApiResponse<object>.Fail("Follow-up not found."));
        return Ok(ApiResponse<object>.Ok(new
        {
            f.FollowUpId, f.Subject, f.FollowUpType, f.FollowUpDate,
            f.Status, f.AssignedTo, f.Remarks, f.CreatedDate
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail(string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))));

        if (dto.FollowUpDate.Date < DateTime.Today)
            return BadRequest(ApiResponse<object>.Fail("Follow-up date cannot be earlier than today."));

        var user = await _userManager.GetUserAsync(User);
        var followUp = new FollowUp
        {
            Subject = dto.Subject, FollowUpType = dto.FollowUpType,
            FollowUpDate = dto.FollowUpDate, Status = "Planned",
            Remarks = dto.Remarks, AssignedTo = dto.AssignedTo ?? user!.Id,
            CustomerId = dto.CustomerId, LeadId = dto.LeadId,
            CreatedBy = user!.Id, CreatedDate = DateTime.UtcNow
        };
        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("Create", "FollowUp", followUp.FollowUpId.ToString(), null, $"Subject: {followUp.Subject}");
        return StatusCode(201, ApiResponse<object>.Ok(new { followUp.FollowUpId }, "Follow-up created."));
    }
}
