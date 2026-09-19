using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BloodDonationNetwork.Api.Middleware;

/// <summary>
/// Maps common EF Core / Postgres save failures to the right HTTP status
/// instead of letting them fall through to the default 500 ProblemDetails.
/// The most common case in this app: a controller passes a client-supplied
/// Guid (organizationId, donorId, ...) straight into an entity and
/// SaveChangesAsync() throws on the FK constraint — that should read as a
/// 400 ("this id doesn't refer to anything real"), not an opaque 500.
///
/// This runs after any exception a controller/service didn't already
/// catch itself — most write paths still have their own explicit
/// try/catch for domain errors (KeyNotFoundException, etc.); this is the
/// backstop for the ones that don't.
/// </summary>
public class DatabaseExceptionHandler : IExceptionHandler
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
