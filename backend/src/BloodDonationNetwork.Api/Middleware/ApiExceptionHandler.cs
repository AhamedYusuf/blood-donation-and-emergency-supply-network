using BloodDonationNetwork.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BloodDonationNetwork.Api.Middleware;

/// <summary>
/// Maps common save failures and authorization exceptions to the right
/// HTTP status instead of letting them fall through to the default 500
/// ProblemDetails. Two cases this covers:
///
/// 1. A controller passes a client-supplied Guid (organizationId,
///    donorId, ...) straight into an entity and SaveChangesAsync() throws
///    on the FK constraint — that should read as a 400 ("this id doesn't
///    refer to anything real"), not an opaque 500.
/// 2. A service throws <see cref="ForbiddenAccessException"/> or
///    <see cref="UnauthorizedAccessException"/> (the org-ownership check
///    pattern used throughout — AppointmentService, RequestService,
///    InventoryService all do this) but the controller action has no
///    try/catch of its own. Found via InventoryController: GetInventory/
///    GetLowStock/GetTransactions/StockRisk call
///    InventoryService.EnsureOrganizationAccessAsync (which throws
///    ForbiddenAccessException for cross-org staff access) with no
///    try/catch at all — a staff member probing another org's inventory
///    got a raw 500 instead of a 403.
///
/// This runs after any exception a controller/service didn't already
/// catch itself — most write paths still have their own explicit
/// try/catch for domain errors; this is the backstop for the ones that
/// don't, and the reason this class deliberately isn't scoped to only
/// database errors despite the similar name it started with.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = Classify(exception);
        if (status is null)
        {
            return false; // not ours — let the default handler produce a 500
        }

        httpContext.Response.StatusCode = status.Value;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://httpstatuses.com/{status}",
            },
            cancellationToken);

        return true;
    }

    private static (int? Status, string Title) Classify(Exception exception)
    {
        if (exception is KeyNotFoundException)
        {
            return (StatusCodes.Status404NotFound, exception.Message);
        }

        if (exception is ForbiddenAccessException)
        {
            return (StatusCodes.Status403Forbidden, exception.Message);
        }

        if (exception is UnauthorizedAccessException)
        {
            return (StatusCodes.Status401Unauthorized, exception.Message);
        }

        if (exception is DbUpdateException { InnerException: PostgresException pg })
        {
            return pg.SqlState switch
            {
                PostgresErrorCodes.ForeignKeyViolation =>
                    (StatusCodes.Status400BadRequest,
                        "One of the referenced ids (organization, user, ...) doesn't exist."),
                PostgresErrorCodes.UniqueViolation =>
                    (StatusCodes.Status409Conflict,
                        "A record with the same unique value already exists."),
                PostgresErrorCodes.NotNullViolation =>
                    (StatusCodes.Status400BadRequest,
                        "A required field was missing."),
                _ => (null, string.Empty),
            };
        }

        return (null, string.Empty);
    }
}
