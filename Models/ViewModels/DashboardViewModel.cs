namespace AcxiomCRM.Models.ViewModels;
public class DashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }
    // Chart data
    public Dictionary<string, int> LeadStatusCounts { get; set; } = new();
    public Dictionary<string, decimal> OpportunityPipelineAmounts { get; set; } = new();
    public Dictionary<string, decimal> MonthlySales { get; set; } = new();
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<AcxiomCRM.Models.AuditLog> RecentActivity { get; set; } = new();
}
