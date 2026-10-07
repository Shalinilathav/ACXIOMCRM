using AcxiomCRM.Data;
using Microsoft.EntityFrameworkCore;
namespace AcxiomCRM.Services;
public class CodeGeneratorService : ICodeGeneratorService
{
    private readonly ApplicationDbContext _context;
    public CodeGeneratorService(ApplicationDbContext context) { _context = context; }
    public async Task<string> GenerateCustomerCodeAsync()
    {
        var count = await _context.Customers.CountAsync();
        return $"CUST-{(count + 1):D4}";
    }
    public async Task<string> GenerateLeadCodeAsync()
    {
        var count = await _context.Leads.CountAsync();
        return $"LEAD-{(count + 1):D4}";
    }
}
