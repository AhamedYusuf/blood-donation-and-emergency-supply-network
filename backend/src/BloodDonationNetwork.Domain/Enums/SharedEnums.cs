namespace BloodDonationNetwork.Domain.Enums;

public static class UserRoles
{
    public const string Donor = "donor";
    public const string Staff = "staff";
    public const string Admin = "admin";
}

public static class BloodTypes
{
    public const string APositive = "A+";
    public const string ANegative = "A-";

    public const string BPositive = "B+";
    public const string BNegative = "B-";

    public const string ABPositive = "AB+";
    public const string ABNegative = "AB-";

    public const string OPositive = "O+";
    public const string ONegative = "O-";
}

public static class RequestStatuses
{
    public const string Open = "open";
    public const string Matching = "matching";
    public const string AwaitingApproval = "awaiting_approval";
    public const string DonorsNotified = "donors_notified";
    public const string PartiallyFulfilled = "partially_fulfilled";
    public const string Fulfilled = "fulfilled";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
}

public static class AppointmentStatuses
{
    public const string Scheduled = "scheduled";
    public const string Completed = "completed";
    public const string NoShow = "no_show";
    public const string Cancelled = "cancelled";
}

public static class WorkflowStatuses
{
    public const string Planning = "planning";
    public const string CheckingStock = "checking_stock";
    public const string Matching = "matching";
    public const string Validating = "validating";
    public const string AwaitingApproval = "awaiting_approval";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string RevisionRequested = "revision_requested";
    public const string Dispatching = "dispatching";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class AgentNames
{
    public const string Coordinator = "coordinator_agent";
    public const string StockCheck = "stock_check_agent";
    public const string MatchingDispatch = "matching_dispatch_agent";
    public const string EligibilityValidation = "eligibility_validation_agent";
}

public static class ApprovalDecisions
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string RevisionRequested = "revision_requested";
}

public static class OrganizationTypes
{
    public const string Hospital = "hospital";
    public const string BloodBank = "blood_bank";
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