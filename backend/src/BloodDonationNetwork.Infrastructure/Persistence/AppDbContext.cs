using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DonorProfile> DonorProfiles => Set<DonorProfile>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<DonationAppointment> DonationAppointments => Set<DonationAppointment>();

    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();

    // Student 2 - Agent workflow tracking
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();

    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    public DbSet<BloodBankInventory> BloodBankInventories => Set<BloodBankInventory>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<DonorDevice> DonorDevices => Set<DonorDevice>();

    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

        // DonationAppointments.DonorId and DonorDevices.DonorUserId store a
        // User.Id (not a DonorProfile.Id — easy to misread given the
        // "Donor" naming; see the comments on those entities). Both were
        // plain, unconstrained Guid columns because DonorProfiles didn't
        // exist yet when DonationAppointments was first migrated. It does
        // now — add the real FK constraints (shadow FK: no CLR navigation
        // property, so nothing that constructs these entities needs to
        // change).
        modelBuilder.Entity<DonationAppointment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.DonorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DonorDevice>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.DonorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Student 2 - AgentWorkflow configuration
        modelBuilder.Entity<AgentWorkflow>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.BloodRequest)
                .WithMany()
                .HasForeignKey(x => x.BloodRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.FailureReason)
                .HasMaxLength(1000);
        });

        // Student 2 - AgentStep configuration
        modelBuilder.Entity<AgentStep>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Workflow)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.AgentName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.StepName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);
        });

        // Student 2 - ApprovalDecision configuration
        modelBuilder.Entity<ApprovalDecision>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Workflow)
                .WithMany(x => x.ApprovalDecisions)
                .HasForeignKey(x => x.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.Decision)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Comments)
                .HasMaxLength(1000);
        });

        modelBuilder.Entity<BloodBankInventory>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.BloodType)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(x => x.UnitsAvailable)
                .IsRequired();

            entity.Property(x => x.LowStockThreshold)
                .IsRequired();

            entity.Property(x => x.LastUpdated)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.OrganizationId,
                x.BloodType
            })
            .IsUnique();
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Inventory)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.TransactionType)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(x => x.Units)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}