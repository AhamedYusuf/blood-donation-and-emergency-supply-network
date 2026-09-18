using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<DonorProfile> DonorProfiles { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<DonationAppointment> DonationAppointments { get; }
    DbSet<BloodRequest> BloodRequests { get; }

    // Student 2 - Agent workflow tracking
    DbSet<AgentWorkflow> AgentWorkflows { get; }
    DbSet<AgentStep> AgentSteps { get; }
    DbSet<ApprovalDecision> ApprovalDecisions { get; }

    DbSet<BloodBankInventory> BloodBankInventories { get; }
DbSet<InventoryTransaction> InventoryTransactions { get; }

    DbSet<DonorDevice> DonorDevices { get; }

    DbSet<StaffInvitation> StaffInvitations { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
