using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Domain.Entities;

public class BloodBankInventory
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } = null!;

    public BloodType BloodType { get; set; }

    public int UnitsAvailable { get; set; }

    public int LowStockThreshold { get; set; }

    public DateTime LastUpdated { get; set; }

    public ICollection<InventoryTransaction> Transactions { get; set; }
        = new List<InventoryTransaction>();
}