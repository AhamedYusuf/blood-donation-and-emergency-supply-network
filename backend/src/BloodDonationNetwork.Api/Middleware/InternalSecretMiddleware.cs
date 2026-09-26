namespace BloodDonationNetwork.Api.Middleware;

public class InternalSecretMiddleware
{
    private readonly RequestDelegate _next;

    private const string HeaderName = "X-Internal-Secret";
    private const string EnvironmentVariableName = "INTERNAL_AGENT_SECRET";

    public InternalSecretMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration)
    {
        if (!context.Request.Path.StartsWithSegments("/api/internal"))
        {
            await _next(context);
            return;
        }

        // Prefer the environment variable.
        // This must match the secret used by the Python agent service.
        var expectedSecret =
            Environment.GetEnvironmentVariable(EnvironmentVariableName);

        // Fallback to ASP.NET configuration.
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            expectedSecret =
                configuration["InternalAgentSecret"]
                ?? configuration[EnvironmentVariableName];
        }

        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsync(
                "Internal agent secret is not configured.");

            return;
        }

        var providedSecret =
            context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedSecret) ||
            !string.Equals(
                providedSecret.Trim(),
                expectedSecret.Trim(),
                StringComparison.Ordinal))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsync(
                "Missing or invalid internal secret.");

            return;
        }

        await _next(context);
    }
}