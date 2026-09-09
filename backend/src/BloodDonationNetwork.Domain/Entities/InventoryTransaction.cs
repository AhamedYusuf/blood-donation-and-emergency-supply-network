using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Domain.Entities;

public class InventoryTransaction
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }

    public BloodBankInventory Inventory { get; set; } = null!;

    public InventoryTransactionType TransactionType { get; set; }

    public int Units { get; set; }

    public Guid? RelatedAppointmentId { get; set; }

    public Guid? RelatedTransferOrgId { get; set; }

    public DateTime CreatedAt { get; set; }
}