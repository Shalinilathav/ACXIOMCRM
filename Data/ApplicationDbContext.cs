using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Lead> Leads { get; set; } = null!;
    public DbSet<Opportunity> Opportunities { get; set; } = null!;
    public DbSet<FollowUp> FollowUps { get; set; } = null!;
    public DbSet<Activity> Activities { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Unique Indexes ────────────────────────────────────────────────────
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Phone)
            .IsUnique();

        modelBuilder.Entity<Lead>()
            .HasIndex(l => l.Email)
            .IsUnique();

        // ── Customer → Leads (restrict delete so we don't orphan data silently) ──
        modelBuilder.Entity<Lead>()
            .HasOne(l => l.Customer)
            .WithMany(c => c.Leads)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Customer → Opportunities ──────────────────────────────────────────
        modelBuilder.Entity<Opportunity>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Opportunities)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Lead → Opportunities (no cascade from Lead side) ──────────────────
        modelBuilder.Entity<Opportunity>()
            .HasOne(o => o.Lead)
            .WithMany()
            .HasForeignKey(o => o.LeadId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Customer → FollowUps ──────────────────────────────────────────────
        modelBuilder.Entity<FollowUp>()
            .HasOne(f => f.Customer)
            .WithMany(c => c.FollowUps)
            .HasForeignKey(f => f.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Lead → FollowUps ──────────────────────────────────────────────────
        modelBuilder.Entity<FollowUp>()
            .HasOne(f => f.Lead)
            .WithMany(l => l.FollowUps)
            .HasForeignKey(f => f.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Opportunity → FollowUps ───────────────────────────────────────────
        modelBuilder.Entity<FollowUp>()
            .HasOne(f => f.Opportunity)
            .WithMany()
            .HasForeignKey(f => f.OpportunityId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Customer → Activities ─────────────────────────────────────────────
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Customer)
            .WithMany(c => c.Activities)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Lead → Activities ─────────────────────────────────────────────────
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Lead)
            .WithMany(l => l.Activities)
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Decimal precision ─────────────────────────────────────────────────
        modelBuilder.Entity<Lead>()
            .Property(l => l.ExpectedValue)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Opportunity>()
            .Property(o => o.Amount)
            .HasColumnType("decimal(18,2)");

        // ── Seed Roles ────────────────────────────────────────────────────────
        var adminRoleId    = "a1b2c3d4-0001-0000-0000-000000000001";
        var managerRoleId  = "a1b2c3d4-0002-0000-0000-000000000002";
        var salesRoleId    = "a1b2c3d4-0003-0000-0000-000000000003";

        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id               = adminRoleId,
                Name             = "Admin",
                NormalizedName   = "ADMIN",
                ConcurrencyStamp = adminRoleId
            },
            new IdentityRole
            {
                Id               = managerRoleId,
                Name             = "Manager",
                NormalizedName   = "MANAGER",
                ConcurrencyStamp = managerRoleId
            },
            new IdentityRole
            {
                Id               = salesRoleId,
                Name             = "SalesExecutive",
                NormalizedName   = "SALESEXECUTIVE",
                ConcurrencyStamp = salesRoleId
            }
        );
    }
}
