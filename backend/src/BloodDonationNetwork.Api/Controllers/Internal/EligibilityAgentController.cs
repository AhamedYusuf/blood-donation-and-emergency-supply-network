using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers.Internal;

public class EligibilityAgentController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public EligibilityAgentController(IApplicationDbContext db)
    {
        _db = db;
    }
}