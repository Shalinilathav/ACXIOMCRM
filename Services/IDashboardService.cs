using AcxiomCRM.Models.ViewModels;
namespace AcxiomCRM.Services;
public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(string userId, string role);
}
