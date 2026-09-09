namespace BloodDonationNetwork.Domain.Enums;

public static class UserRoles
{
    public const string Donor = "donor";
    public const string Staff = "staff";
    public const string Admin = "admin";
}

public static class BloodTypes
{
    public const string APositive = "A+", ANegative = "A-";
    public const string BPositive = "B+", BNegative = "B-";
    public const string ABPositive = "AB+", ABNegative = "AB-";
    public const string OPositive = "O+", ONegative = "O-";
}

public static class RequestStatuses
{
    public const string Open = "open", Matching = "matching", AwaitingApproval = "awaiting_approval",
        DonorsNotified = "donors_notified", PartiallyFulfilled = "partially_fulfilled",
        Fulfilled = "fulfilled", Expired = "expired", Cancelled = "cancelled";
}

public static class AppointmentStatuses
{
    public const string Scheduled = "scheduled", Completed = "completed", NoShow = "no_show", Cancelled = "cancelled";
}

public static class WorkflowStatuses
{
    public const string Planning = "planning", CheckingStock = "checking_stock", Matching = "matching",
        Validating = "validating", AwaitingApproval = "awaiting_approval", Approved = "approved",
        Rejected = "rejected", RevisionRequested = "revision_requested", Dispatching = "dispatching",
        Completed = "completed", Failed = "failed";
}

public static class AgentNames
{
    public const string Coordinator = "coordinator_agent", StockCheck = "stock_check_agent",
        MatchingDispatch = "matching_dispatch_agent", EligibilityValidation = "eligibility_validation_agent";
}

public static class ApprovalDecisions
{
    public const string Approved = "approved", Rejected = "rejected", RevisionRequested = "revision_requested";
}

public static class OrganizationTypes
{
    public const string Hospital = "hospital", BloodBank = "blood_bank";
}

public enum BloodType
{
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
    OPositive,
    ONegative
}

public enum RequestUrgency
{
    Normal,
    Urgent,
    Critical
}

public enum BloodRequestStatus
{
    Pending,
    AwaitingApproval,
    Approved,
    Dispatched,
    Fulfilled,
    Closed,
    Cancelled
}