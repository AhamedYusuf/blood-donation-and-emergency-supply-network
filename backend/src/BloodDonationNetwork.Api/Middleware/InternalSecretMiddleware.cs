namespace BloodDonationNetwork.Api.Middleware;

public class InternalSecretMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Internal-Secret";

    public InternalSecretMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration)
    {
        if (context.Request.Path.StartsWithSegments("/api/internal"))
        {
            // Support both:
            // 1. InternalAgentSecret
            // 2. INTERNAL_AGENT_SECRET
            var expectedSecret =
                configuration["InternalAgentSecret"]
                ?? configuration["INTERNAL_AGENT_SECRET"];

            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                throw new InvalidOperationException(
                    "InternalAgentSecret is not configured.");
            }

            var providedSecret =
                context.Request.Headers[HeaderName].FirstOrDefault();

            if (string.IsNullOrEmpty(providedSecret) ||
                providedSecret != expectedSecret)
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsync(
                    "Missing or invalid internal secret.");

                return;
            }
        }

        await _next(context);
    }
}