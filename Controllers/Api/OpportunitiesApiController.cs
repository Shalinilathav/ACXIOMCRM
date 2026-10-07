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
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Prospecting", "Qualification", "Needs Analysis", "Value Proposition",
        "Id. Decision Makers", "Perception Analysis", "Proposal/Price Quote",
        "Negotiation/Review", "Closed Won", "Closed Lost"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICodeGeneratorService _codeGen;
    private readonly IAuditService _audit;

    public OpportunitiesApiController(
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

    // GET /api/opportunities?search=...
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        IQueryable<Opportunity> query = _context.Opportunities
            .Include(o => o.Customer);

        if (!isAdminOrManager)
            query = query.Where(o => o.AssignedTo == currentUser.UserName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(s) ||
                o.Stage.ToLower().Contains(s) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)));
        }

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityDto
            {
                OpportunityId     = o.OpportunityId,
                OpportunityName   = o.OpportunityName,
                CustomerId        = o.CustomerId,
                CustomerName      = o.Customer != null ? o.Customer.CustomerName : null,
                Stage             = o.Stage,
                Amount            = o.Amount,
                Probability       = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status            = o.Status,
                AssignedTo        = o.AssignedTo,
                WeightedAmount    = o.Amount * o.Probability / 100m,
                CreatedDate       = o.CreatedDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<OpportunityDto>>.Ok(opportunities, $"{opportunities.Count} opportunity(s) found."));
    }

    // GET /api/opportunities/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound(ApiResponse<object>.Fail($"Opportunity with ID {id} not found."));

        if (!isAdminOrManager && opportunity.AssignedTo != currentUser.UserName)
            return Forbid();

        var dto = new OpportunityDto
        {
            OpportunityId     = opportunity.OpportunityId,
            OpportunityName   = opportunity.OpportunityName,
            CustomerId        = opportunity.CustomerId,
            CustomerName      = opportunity.Customer?.CustomerName,
            Stage             = opportunity.Stage,
            Amount            = opportunity.Amount,
            Probability       = opportunity.Probability,
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            Status            = opportunity.Status,
            AssignedTo        = opportunity.AssignedTo,
            WeightedAmount    = opportunity.Amount * opportunity.Probability / 100m,
            CreatedDate       = opportunity.CreatedDate
        };

        return Ok(ApiResponse<OpportunityDto>.Ok(dto));
    }

    // POST /api/opportunities
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        var validationErrors = new List<string>();

        // Validate Amount
        if (dto.Amount <= 0)
            validationErrors.Add("Amount must be greater than 0.");

        // Validate Probability
        if (dto.Probability < 0 || dto.Probability > 100)
            validationErrors.Add("Probability must be between 0 and 100.");

        // Validate ExpectedCloseDate >= today
        if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            validationErrors.Add("Expected close date must be today or a future date.");

        // Validate Stage
        if (!AllowedStages.Contains(dto.Stage))
            validationErrors.Add($"Invalid stage '{dto.Stage}'. Allowed values: {string.Join(", ", AllowedStages)}.");

        if (validationErrors.Any())
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", validationErrors)));

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        // Validate CustomerId if provided
        if (dto.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers
                .AnyAsync(c => c.CustomerId == dto.CustomerId.Value && c.Status == "Active");
            if (!customerExists)
                return BadRequest(ApiResponse<object>.Fail($"Customer with ID {dto.CustomerId} does not exist or is inactive."));
        }

        // Determine status from stage
        var status = dto.Stage switch
        {
            "Closed Won"  => "Won",
            "Closed Lost" => "Lost",
            _             => "Open"
        };

        var opportunity = new Opportunity
        {
            OpportunityName   = dto.OpportunityName,
            CustomerId        = dto.CustomerId,
            Stage             = dto.Stage,
            Amount            = dto.Amount,
            Probability       = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Notes             = dto.Notes,
            AssignedTo        = dto.AssignedTo ?? currentUser.UserName,
            Status            = status,
            CreatedDate       = DateTime.UtcNow,
            CreatedBy = currentUser.Id
        };

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("Create", "Opportunity", opportunity.OpportunityId.ToString(), null, $"Created opportunity '{opportunity.OpportunityName}' (Stage: {opportunity.Stage}, Amount: {opportunity.Amount:C}).");

        // Reload with customer for response
        var created = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OpportunityId == opportunity.OpportunityId);

        var resultDto = new OpportunityDto
        {
            OpportunityId     = opportunity.OpportunityId,
            OpportunityName   = opportunity.OpportunityName,
            CustomerId        = opportunity.CustomerId,
            CustomerName      = created?.Customer?.CustomerName,
            Stage             = opportunity.Stage,
            Amount            = opportunity.Amount,
            Probability       = opportunity.Probability,
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            Status            = opportunity.Status,
            AssignedTo        = opportunity.AssignedTo,
            WeightedAmount    = opportunity.Amount * opportunity.Probability / 100m,
            CreatedDate       = opportunity.CreatedDate
        };

        return CreatedAtAction(nameof(GetById), new { id = opportunity.OpportunityId },
            ApiResponse<OpportunityDto>.Ok(resultDto, "Opportunity created successfully."));
    }

    // PUT /api/opportunities/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        var validationErrors = new List<string>();

        if (dto.Amount <= 0)
            validationErrors.Add("Amount must be greater than 0.");

        if (dto.Probability < 0 || dto.Probability > 100)
            validationErrors.Add("Probability must be between 0 and 100.");

        if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            validationErrors.Add("Expected close date must be today or a future date.");

        if (!AllowedStages.Contains(dto.Stage))
            validationErrors.Add($"Invalid stage '{dto.Stage}'. Allowed values: {string.Join(", ", AllowedStages)}.");

        if (validationErrors.Any())
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", validationErrors)));

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound(ApiResponse<object>.Fail($"Opportunity with ID {id} not found."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");
        if (!isAdminOrManager && opportunity.AssignedTo != currentUser.UserName)
            return Forbid();

        // Validate CustomerId if provided
        if (dto.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers
                .AnyAsync(c => c.CustomerId == dto.CustomerId.Value && c.Status == "Active");
            if (!customerExists)
                return BadRequest(ApiResponse<object>.Fail($"Customer with ID {dto.CustomerId} does not exist or is inactive."));
        }

        var status = dto.Stage switch
        {
            "Closed Won"  => "Won",
            "Closed Lost" => "Lost",
            _             => "Open"
        };

        var oldName = opportunity.OpportunityName;
        opportunity.OpportunityName   = dto.OpportunityName;
        opportunity.CustomerId        = dto.CustomerId;
        opportunity.Stage             = dto.Stage;
        opportunity.Amount            = dto.Amount;
        opportunity.Probability       = dto.Probability;
        opportunity.ExpectedCloseDate = dto.ExpectedCloseDate;
        opportunity.Notes             = dto.Notes;
        opportunity.AssignedTo        = dto.AssignedTo ?? opportunity.AssignedTo;
        opportunity.Status            = status;
await _context.SaveChangesAsync();

        // Reload customer name
        await _context.Entry(opportunity).Reference(o => o.Customer).LoadAsync();

        await _audit.LogAsync("Update", "Opportunity", opportunity.OpportunityId.ToString(), null, $"Updated opportunity '{oldName}' → '{opportunity.OpportunityName}' (Stage: {opportunity.Stage}).");

        var resultDto = new OpportunityDto
        {
            OpportunityId     = opportunity.OpportunityId,
            OpportunityName   = opportunity.OpportunityName,
            CustomerId        = opportunity.CustomerId,
            CustomerName      = opportunity.Customer?.CustomerName,
            Stage             = opportunity.Stage,
            Amount            = opportunity.Amount,
            Probability       = opportunity.Probability,
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            Status            = opportunity.Status,
            AssignedTo        = opportunity.AssignedTo,
            WeightedAmount    = opportunity.Amount * opportunity.Probability / 100m,
            CreatedDate       = opportunity.CreatedDate
        };

        return Ok(ApiResponse<OpportunityDto>.Ok(resultDto, "Opportunity updated successfully."));
    }

    // DELETE /api/opportunities/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null)
            return NotFound(ApiResponse<object>.Fail($"Opportunity with ID {id} not found."));

        opportunity.Status       = "Cancelled";
await _context.SaveChangesAsync();

        await _audit.LogAsync("Delete", "Opportunity", opportunity.OpportunityId.ToString(), null, $"Cancelled opportunity '{opportunity.OpportunityName}'.");

        return Ok(ApiResponse<object>.Ok(new { id = opportunity.OpportunityId }, "Opportunity cancelled successfully."));
    }
}
