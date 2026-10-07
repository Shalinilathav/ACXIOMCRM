using AcxiomCRM.Data;
using AcxiomCRM.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string userId, string role)
    {
        bool isSalesExec = role == "SalesExecutive";
        var today = DateTime.UtcNow.Date;

        // ── Customers ───────────────────────────────────────────────────────────
        var customerQuery = _context.Customers.AsQueryable();
        if (isSalesExec)
            customerQuery = customerQuery.Where(c => c.AssignedTo == userId);
        int totalCustomers = await customerQuery.CountAsync();

        // ── Leads ───────────────────────────────────────────────────────────────
        var leadQuery = _context.Leads.AsQueryable();
        if (isSalesExec)
            leadQuery = leadQuery.Where(l => l.AssignedTo == userId);
        int totalLeads = await leadQuery.CountAsync();
        int openLeads = await leadQuery
            .Where(l => l.Status != "Converted" && l.Status != "Lost")
            .CountAsync();

        // Lead status distribution
        var leadStatusGroups = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var leadStatusCounts = leadStatusGroups.ToDictionary(g => g.Status ?? "Unknown", g => g.Count);

        // ── Opportunities ───────────────────────────────────────────────────────
        var oppQuery = _context.Opportunities.AsQueryable();
        if (isSalesExec)
            oppQuery = oppQuery.Where(o => o.AssignedTo == userId);
        int totalOpportunities = await oppQuery.CountAsync();
        int openOpportunities = await oppQuery.Where(o => o.Status == "Open").CountAsync();
        int wonOpportunities  = await oppQuery.Where(o => o.Stage == "Won").CountAsync();
        int lostOpportunities = await oppQuery.Where(o => o.Stage == "Lost").CountAsync();
        var openOpps = await oppQuery.Where(o => o.Status == "Open")
            .Select(o => new { o.Amount, o.Stage }).ToListAsync();
        decimal totalPipelineValue = openOpps.Sum(o => o.Amount);

        // Opportunity pipeline amounts by stage
        var opportunityPipelineAmounts = (await oppQuery
            .Select(o => new { o.Stage, o.Amount }).ToListAsync())
            .GroupBy(o => o.Stage ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Amount));

        // Monthly sales – last 6 months of Won opportunities
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var wonOpps = await oppQuery
            .Where(o => o.Stage == "Won" && o.ExpectedCloseDate >= sixMonthsAgo)
            .Select(o => new { o.ExpectedCloseDate, o.Amount })
            .ToListAsync();

        var monthlySales = wonOpps
            .GroupBy(o => o.ExpectedCloseDate.ToString("MMM yyyy"))
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Amount));

        // Fill in missing months (so chart always has 6 data points)
        var monthlySalesOrdered = new Dictionary<string, decimal>();
        for (int i = 5; i >= 0; i--)
        {
            var key = DateTime.UtcNow.AddMonths(-i).ToString("MMM yyyy");
            monthlySalesOrdered[key] = monthlySales.TryGetValue(key, out var val) ? val : 0m;
        }

        // ── Follow-Ups ──────────────────────────────────────────────────────────
        var followUpQuery = _context.FollowUps.AsQueryable();
        if (isSalesExec)
            followUpQuery = followUpQuery.Where(f => f.AssignedTo == userId);

        int pendingFollowUps = await followUpQuery
            .Where(f => f.FollowUpDate >= today && f.Status == "Planned")
            .CountAsync();
        int overdueFollowUps = await followUpQuery
            .Where(f => f.FollowUpDate < today && f.Status == "Planned")
            .CountAsync();

        var upcomingFollowUps = await followUpQuery
            .Where(f => f.FollowUpDate >= today && f.Status == "Planned")
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
            .ToListAsync();

        // ── Recent Audit Activity ───────────────────────────────────────────────
        var recentActivity = await _context.AuditLogs
            .OrderByDescending(a => a.CreatedDate)
            .Take(10)
            .ToListAsync();

        return new DashboardViewModel
        {
            TotalCustomers            = totalCustomers,
            TotalLeads                = totalLeads,
            OpenLeads                 = openLeads,
            TotalOpportunities        = totalOpportunities,
            OpenOpportunities         = openOpportunities,
            WonOpportunities          = wonOpportunities,
            LostOpportunities         = lostOpportunities,
            TotalPipelineValue        = totalPipelineValue,
            PendingFollowUps          = pendingFollowUps,
            OverdueFollowUps          = overdueFollowUps,
            LeadStatusCounts          = leadStatusCounts,
            OpportunityPipelineAmounts = opportunityPipelineAmounts,
            MonthlySales              = monthlySalesOrdered,
            UpcomingFollowUps         = upcomingFollowUps,
            RecentActivity            = recentActivity
        };
    }
}
