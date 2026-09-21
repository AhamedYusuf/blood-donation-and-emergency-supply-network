using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DonorProfile> DonorProfiles =>
        Set<DonorProfile>();

    public DbSet<Organization> Organizations =>
        Set<Organization>();

    public DbSet<User> Users =>
        Set<User>();

    public DbSet<DonationAppointment> DonationAppointments =>
        Set<DonationAppointment>();

    public DbSet<BloodRequest> BloodRequests =>
        Set<BloodRequest>();

    // Student 2 - Agent workflow tracking
    public DbSet<AgentWorkflow> AgentWorkflows =>
        Set<AgentWorkflow>();

    public DbSet<AgentStep> AgentSteps =>
        Set<AgentStep>();

    public DbSet<ApprovalDecision> ApprovalDecisions =>
        Set<ApprovalDecision>();

    public DbSet<BloodBankInventory> BloodBankInventories =>
        Set<BloodBankInventory>();

    public DbSet<InventoryTransaction> InventoryTransactions =>
        Set<InventoryTransaction>();

    public DbSet<DonorDevice> DonorDevices =>
        Set<DonorDevice>();

    public DbSet<StaffInvitation> StaffInvitations =>
        Set<StaffInvitation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

        // DonationAppointments.DonorId and
        // DonorDevices.DonorUserId store User.Id.
        modelBuilder.Entity<DonationAppointment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.DonorId)
            .OnDelete(DeleteBehavior.Restrict);

        // RelatedWorkflowId is nullable because an appointment
        // can exist without being created by an agent workflow.
        modelBuilder.Entity<DonationAppointment>()
            .HasOne<AgentWorkflow>()
            .WithMany()
            .HasForeignKey(a => a.RelatedWorkflowId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DonorDevice>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.DonorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================
        // STUDENT 2 - AGENT WORKFLOW
        // =====================================================

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

            entity.Property(x => x.Objective)
                .IsRequired();

            entity.Property(x => x.CurrentAgent)
                .HasMaxLength(100);

            entity.Property(x => x.FailureReason)
                .HasMaxLength(1000);
        });

        // =====================================================
        // STUDENT 2 - AGENT STEP
        // =====================================================

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

            /*
             * Keep the existing C# property names:
             *
             * InputJson
             * OutputJson
             * RetryCount
             *
             * Other parts of the project already depend on these
             * property names.
             *
             * Only map their DATABASE column names to the names
             * required by the technical specification.
             */

            entity.Property(x => x.InputJson)
                .HasColumnName("input_data");

            entity.Property(x => x.OutputJson)
                .HasColumnName("output_data");

            entity.Property(x => x.RetryCount)
                .HasColumnName("retry_count")
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);
        });

        // =====================================================
        // STUDENT 2 - APPROVAL DECISION
        // =====================================================

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

        // =====================================================
        // INVENTORY
        // =====================================================

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

        // =====================================================
        // INVENTORY TRANSACTIONS
        // =====================================================

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