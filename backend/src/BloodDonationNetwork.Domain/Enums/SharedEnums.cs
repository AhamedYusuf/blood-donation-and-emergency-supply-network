namespace BloodDonationNetwork.Domain.Enums;

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