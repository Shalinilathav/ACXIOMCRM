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
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICodeGeneratorService _codeGen;
    private readonly IAuditService _audit;

    public CustomersApiController(
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

    // GET /api/customers?search=...
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        IQueryable<Customer> query = _context.Customers;

        // Scope by role: Sales users only see their own customers
        if (!isAdminOrManager)
            query = query.Where(c => c.AssignedTo == currentUser.UserName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(s) ||
                c.Email.ToLower().Contains(s) ||
                c.Phone.Contains(s) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                (c.City != null && c.City.ToLower().Contains(s)) ||
                c.CustomerCode.ToLower().Contains(s));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerDto
            {
                CustomerId  = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email        = c.Email,
                Phone        = c.Phone,
                CompanyName  = c.CompanyName,
                Address      = c.Address,
                City         = c.City,
                State        = c.State,
                Status       = c.Status,
                AssignedTo   = c.AssignedTo,
                CreatedDate  = c.CreatedDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CustomerDto>>.Ok(customers, $"{customers.Count} customer(s) found."));
    }

    // GET /api/customers/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound(ApiResponse<object>.Fail($"Customer with ID {id} not found."));

        // Sales users can only view their own customers
        if (!isAdminOrManager && customer.AssignedTo != currentUser.UserName)
            return Forbid();

        var dto = new CustomerDto
        {
            CustomerId   = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email        = customer.Email,
            Phone        = customer.Phone,
            CompanyName  = customer.CompanyName,
            Address      = customer.Address,
            City         = customer.City,
            State        = customer.State,
            Status       = customer.Status,
            AssignedTo   = customer.AssignedTo,
            CreatedDate  = customer.CreatedDate
        };

        return Ok(ApiResponse<CustomerDto>.Ok(dto));
    }

    // POST /api/customers
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        // Check email uniqueness
        var emailExists = await _context.Customers
            .AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower());
        if (emailExists)
            return Conflict(ApiResponse<object>.Fail($"A customer with email '{dto.Email}' already exists."));

        // Check phone uniqueness
        var phoneExists = await _context.Customers
            .AnyAsync(c => c.Phone == dto.Phone);
        if (phoneExists)
            return Conflict(ApiResponse<object>.Fail($"A customer with phone '{dto.Phone}' already exists."));

        var code = await _codeGen.GenerateCustomerCodeAsync();

        var customer = new Customer
        {
            CustomerCode = code,
            CustomerName = dto.CustomerName,
            Email        = dto.Email,
            Phone        = dto.Phone,
            CompanyName  = dto.CompanyName,
            Address      = dto.Address,
            City         = dto.City,
            State        = dto.State,
            Notes        = dto.Notes,
            AssignedTo   = dto.AssignedTo ?? currentUser.UserName,
            Status       = "Active",
            CreatedDate  = DateTime.UtcNow,
            CreatedBy = currentUser.Id
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null, "Created customer '{customer.CustomerName}' ({customer.CustomerCode}).");

        var resultDto = new CustomerDto
        {
            CustomerId   = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email        = customer.Email,
            Phone        = customer.Phone,
            CompanyName  = customer.CompanyName,
            Address      = customer.Address,
            City         = customer.City,
            State        = customer.State,
            Status       = customer.Status,
            AssignedTo   = customer.AssignedTo,
            CreatedDate  = customer.CreatedDate
        };

        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId },
            ApiResponse<CustomerDto>.Ok(resultDto, "Customer created successfully."));
    }

    // PUT /api/customers/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", errors)));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound(ApiResponse<object>.Fail($"Customer with ID {id} not found."));

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");
        if (!isAdminOrManager && customer.AssignedTo != currentUser.UserName)
            return Forbid();

        // Check email uniqueness, excluding self
        var emailExists = await _context.Customers
            .AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower() && c.CustomerId != id);
        if (emailExists)
            return Conflict(ApiResponse<object>.Fail($"Another customer with email '{dto.Email}' already exists."));

        // Check phone uniqueness, excluding self
        var phoneExists = await _context.Customers
            .AnyAsync(c => c.Phone == dto.Phone && c.CustomerId != id);
        if (phoneExists)
            return Conflict(ApiResponse<object>.Fail($"Another customer with phone '{dto.Phone}' already exists."));

        var oldName = customer.CustomerName;
        customer.CustomerName = dto.CustomerName;
        customer.Email        = dto.Email;
        customer.Phone        = dto.Phone;
        customer.CompanyName  = dto.CompanyName;
        customer.Address      = dto.Address;
        customer.City         = dto.City;
        customer.State        = dto.State;
        customer.Status       = dto.Status;
        customer.Notes        = dto.Notes;
        customer.AssignedTo   = dto.AssignedTo ?? customer.AssignedTo;
await _context.SaveChangesAsync();

        await _audit.LogAsync("Update", "Customer", customer.CustomerId.ToString(), null, "Updated customer '{oldName}' → '{customer.CustomerName}' ({customer.CustomerCode}).");

        var resultDto = new CustomerDto
        {
            CustomerId   = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email        = customer.Email,
            Phone        = customer.Phone,
            CompanyName  = customer.CompanyName,
            Address      = customer.Address,
            City         = customer.City,
            State        = customer.State,
            Status       = customer.Status,
            AssignedTo   = customer.AssignedTo,
            CreatedDate  = customer.CreatedDate
        };

        return Ok(ApiResponse<CustomerDto>.Ok(resultDto, "Customer updated successfully."));
    }

    // DELETE /api/customers/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated."));

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound(ApiResponse<object>.Fail($"Customer with ID {id} not found."));

        // Soft-delete: set status to Inactive instead of hard delete
        customer.Status       = "Inactive";
await _context.SaveChangesAsync();

        await _audit.LogAsync("Delete", "Customer", customer.CustomerId.ToString(), null, "Deactivated customer '{customer.CustomerName}' ({customer.CustomerCode}).");

        return Ok(ApiResponse<object>.Ok(new { id = customer.CustomerId }, "Customer deactivated successfully."));
    }
}
