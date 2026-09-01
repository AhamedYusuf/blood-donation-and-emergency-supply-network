
namespace BloodDonationNetwork.Application.DTOs.Donors;

public class DonorRegisterRequest { 
    public string BloodType {get;set;} = default!; 
    public DateOnly DateOfBirth {get;set;} 
    public string? Address {get;set;} 
    public Dictionary<string,bool>? MedicalFlags {get;set;} 
}

public class DonorUpdateRequest { 
    public string? Address {get;set;} 
    public Dictionary<string,bool>? MedicalFlags {get;set;} 
    public DateOnly? LastDonationDate {get;set;} 
}

public class DonorProfileResponse { 
    public Guid Id {get;set;} 
    public Guid UserId {get;set;} 
    public string BloodType {get;set;} = default!; 
    public string EligibilityStatus {get;set;} = default!; 
    public DateOnly DateOfBirth {get;set;} 
    public DateOnly? LastDonationDate {get;set;} 
    public string? Address {get;set;} 
    public double? Latitude {get;set;} 
    public double? Longitude {get;set;} 
    public bool LocationVerified {get;set;} 
    public bool VerifiedByAdmin {get;set;} 
}

public class EligibilityResponse { 
    public bool IsEligible {get;set;} 
    public string? Reason {get;set;}
    public int? DaysUntilEligible {get;set;} 
}