namespace BloodDonationNetwork.Application.Common;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}