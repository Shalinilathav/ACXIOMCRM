namespace AcxiomCRM.Services;
public interface ICodeGeneratorService
{
    Task<string> GenerateCustomerCodeAsync();
    Task<string> GenerateLeadCodeAsync();
}
