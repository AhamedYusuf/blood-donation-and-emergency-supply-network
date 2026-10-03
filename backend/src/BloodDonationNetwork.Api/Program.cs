using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Infrastructure.ExternalClients;
using BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// CORS
// =====================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<
    BloodDonationNetwork.Api.Middleware.ApiExceptionHandler>();

// =====================================================
// EXTERNAL CLIENTS
// =====================================================

builder.Services.AddHttpClient<IGeocodingClient, LocationIqClient>(c =>
    c.DefaultRequestHeaders.Add(
        "User-Agent",
        "BloodDonationNetwork/1.0"));

// Python Agent Service
builder.Services.AddHttpClient("AgentService", client =>
{
    var agentServiceBaseUrl =
        builder.Configuration["AgentService:BaseUrl"]
        ?? "http://localhost:8000";
    if (!Uri.TryCreate(
            agentServiceBaseUrl,
            UriKind.Absolute,
            out var agentServiceUri) ||
        (agentServiceUri.Scheme != Uri.UriSchemeHttp &&
         agentServiceUri.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            "AgentService:BaseUrl must be an absolute HTTP or HTTPS URL.");
    }

    client.BaseAddress = agentServiceUri;

    var internalAgentSecret =
        builder.Configuration["InternalAgentSecret"]
        ?? builder.Configuration["INTERNAL_AGENT_SECRET"];

    if (string.IsNullOrWhiteSpace(internalAgentSecret))
    {
        throw new InvalidOperationException(
            "InternalAgentSecret must be configured for AgentService calls.");
    }

    client.DefaultRequestHeaders.Add(
        "X-Internal-Secret",
        internalAgentSecret.Trim());
});

// =====================================================
// PUSH NOTIFICATIONS (Firebase Cloud Messaging)
// =====================================================
// If no service-account key is configured the app still runs — a no-op
// sender is used and NotificationService reports "not configured".

var fcmOptions =
    builder.Configuration.GetSection("Fcm").Get<FcmOptions>()
    ?? new FcmOptions();

if (fcmOptions.HasCredentials)
{
    var keyJson =
        !string.IsNullOrWhiteSpace(
            fcmOptions.ServiceAccountKeyJson)
            ? fcmOptions.ServiceAccountKeyJson!
            : File.ReadAllText(
                fcmOptions.ServiceAccountKeyPath!);

    var serviceAccount =
        ServiceAccountKey.Parse(keyJson);

    var projectId =
        fcmOptions.ProjectId
        ?? serviceAccount.ProjectId;

    builder.Services.AddHttpClient("fcm-token");
    builder.Services.AddHttpClient("fcm-send");

    builder.Services.AddSingleton(sp =>
        new GoogleAccessTokenProvider(
            serviceAccount,
            sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient("fcm-token")));

    builder.Services.AddScoped<IFcmSender>(sp =>
        new FcmHttpSender(
            sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient("fcm-send"),
            sp.GetRequiredService<
                GoogleAccessTokenProvider>(),
            projectId,
            TimeSpan.FromSeconds(
                fcmOptions.RequestTimeoutSeconds)));
}
else
{
    builder.Services.AddScoped<
        IFcmSender,
        NoOpFcmSender>();
}

// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<
    INotificationService,
    NotificationService>();

builder.Services.AddScoped<
    IDonorService,
    DonorService>();

builder.Services.AddScoped<
    IInventoryService,
    InventoryService>();

builder.Services.AddScoped<
    IOrganizationService,
    OrganizationService>();

builder.Services.AddScoped<DonorRankingCalculator>();

builder.Services.AddScoped<
    IMatchingDispatchAgentService,
    MatchingDispatchAgentService>();

builder.Services.AddScoped<
    IEligibilityRuleEngine,
    EligibilityRuleEngine>();

builder.Services.AddScoped<
    IRequestService,
    RequestService>();

builder.Services.AddScoped<
    IAgentWorkflowService,
    AgentWorkflowService>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IStaffInvitationService,
    StaffInvitationService>();

builder.Services.AddScoped<
    IAppointmentService,
    AppointmentService>();

// =====================================================
// DATABASE
// =====================================================

var connectionString =
    builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Render's Blueprint only hands a managed Postgres instance's
    // combined connection string to env vars as a postgres:// URI,
    // which Npgsql's connection-string parser does not accept (it wants
    // Host=...;Port=...;... keyword=value pairs). Assembling it from the
    // individual host/port/database/user/password properties Render
    // also exposes avoids that entirely. Local dev is unaffected —
    // appsettings.Development.json's ConnectionStrings:Default is
    // already in the right format, so this path is never reached there.
    var pgHost = builder.Configuration["Database:Host"];

    if (!string.IsNullOrWhiteSpace(pgHost))
    {
        var pgPort = builder.Configuration["Database:Port"] ?? "5432";
        var pgDatabase = builder.Configuration["Database:Name"];
        var pgUser = builder.Configuration["Database:User"];
        var pgPassword = builder.Configuration["Database:Password"];

        connectionString =
            $"Host={pgHost};Port={pgPort};Database={pgDatabase};" +
            $"Username={pgUser};Password={pgPassword};" +
            "SSL Mode=Require;Trust Server Certificate=true";
    }
}

var dataSourceBuilder =
    new Npgsql.NpgsqlDataSourceBuilder(connectionString);

dataSourceBuilder.EnableDynamicJson();

var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseNpgsql(dataSource));

builder.Services.AddScoped<IApplicationDbContext>(
    sp => sp.GetRequiredService<AppDbContext>());

// =====================================================
// JWT AUTHENTICATION
// =====================================================

builder.Services.AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration[
                        "Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration[
                        "Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration[
                                "Jwt:Key"]!)),
            };
    });

// =====================================================
// AUTHORIZATION
// =====================================================

builder.Services.AddAuthorization();

// =====================================================
// CONTROLLERS
// =====================================================

builder.Services.AddControllers();

// =====================================================
// SWAGGER / OPENAPI
// =====================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models
            .OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type =
                Microsoft.OpenApi.Models
                    .SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In =
                Microsoft.OpenApi.Models
                    .ParameterLocation.Header,
            Description =
                "Enter your JWT token. Example: Bearer {your token}"
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models
            .OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models
                    .OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models
                            .OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models
                                    .ReferenceType
                                    .SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

// =====================================================
// HTTP REQUEST PIPELINE
// =====================================================

app.UseExceptionHandler();

app.UseCors("DevelopmentCors");

// Swagger, migrations and admin-seeding used to be gated behind
// IsDevelopment() — harmless locally, but fatal for a real deployment:
// ASPNETCORE_ENVIRONMENT is "Production" there, so none of this would
// ever run. The deployed database would stay unmigrated (every request
// failing on missing tables) and the assignment spec's required
// "working Swagger URL" would 404. Both now run in every environment.
app.UseSwagger();
app.UseSwaggerUI();

using (var seedScope = app.Services.CreateScope())
{
    var seedContext =
        seedScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await seedContext.Database.MigrateAsync();

    if (!await seedContext.Users.AnyAsync(
            u =>
                u.Role ==
                BloodDonationNetwork.Domain.Entities
                    .UserRole.Admin))
    {
        string adminEmail;
        string adminPassword;

        if (app.Environment.IsDevelopment())
        {
            // Convenience defaults for a local dev DB only — never used
            // outside Development, so never reaches a real deployment.
            adminEmail =
                (
                    app.Configuration["Seed:AdminEmail"]
                    ?? "admin@blooddonation.local"
                )
                .Trim()
                .ToLowerInvariant();

            adminPassword =
                app.Configuration["Seed:AdminPassword"]
                ?? "ChangeMe123!";
        }
        else
        {
            // A well-known fallback password would let anyone who reads
            // this source seed themselves an admin account on the real
            // deployed instance. Refuse to start instead of silently
            // creating one.
            adminEmail =
                app.Configuration["Seed:AdminEmail"]?.Trim().ToLowerInvariant()
                ?? throw new InvalidOperationException(
                    "Seed:AdminEmail (env var Seed__AdminEmail) must be " +
                    "set outside Development — no default admin account " +
                    "will be created without it.");

            adminPassword =
                app.Configuration["Seed:AdminPassword"]
                ?? throw new InvalidOperationException(
                    "Seed:AdminPassword (env var Seed__AdminPassword) " +
                    "must be set outside Development — no default admin " +
                    "account will be created without it.");
        }

        seedContext.Users.Add(
            new BloodDonationNetwork.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        adminPassword),
                Role =
                    BloodDonationNetwork.Domain.Entities
                        .UserRole.Admin,
                FullName = "Admin",
                PhoneNumber = string.Empty,
                OrganizationId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

        await seedContext.SaveChangesAsync();

        app.Logger.LogWarning(
            "Seeded first admin account {Email} — " +
            "set Seed:AdminEmail/Seed:AdminPassword to override, " +
            "and change this password after logging in.",
            adminEmail);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.UseHttpsRedirection();

app.UseMiddleware<
    BloodDonationNetwork.Api.Middleware
        .InternalSecretMiddleware>();

// Authentication must come before Authorization
app.UseAuthentication();

app.UseAuthorization();

// Map API controllers
app.MapControllers();

app.Run();