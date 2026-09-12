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
builder.Services.AddExceptionHandler<BloodDonationNetwork.Api.Middleware.DatabaseExceptionHandler>();

// =====================================================
// EXTERNAL CLIENTS
// =====================================================

builder.Services.AddHttpClient<IGeocodingClient, NominatimClient>(c =>
    c.DefaultRequestHeaders.Add(
        "User-Agent",
        "BloodDonationNetwork/1.0"));

// =====================================================
// PUSH NOTIFICATIONS (Firebase Cloud Messaging)
// =====================================================
// If no service-account key is configured the app still runs — a no-op
// sender is used and NotificationService reports "not configured".

var fcmOptions = builder.Configuration.GetSection("Fcm").Get<FcmOptions>() ?? new FcmOptions();

if (fcmOptions.HasCredentials)
{
    var keyJson = !string.IsNullOrWhiteSpace(fcmOptions.ServiceAccountKeyJson)
        ? fcmOptions.ServiceAccountKeyJson!
        : File.ReadAllText(fcmOptions.ServiceAccountKeyPath!);
    var serviceAccount = ServiceAccountKey.Parse(keyJson);
    var projectId = fcmOptions.ProjectId ?? serviceAccount.ProjectId;

    builder.Services.AddHttpClient("fcm-token");
    builder.Services.AddHttpClient("fcm-send");

    builder.Services.AddSingleton(sp => new GoogleAccessTokenProvider(
        serviceAccount,
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("fcm-token")));

    builder.Services.AddScoped<IFcmSender>(sp => new FcmHttpSender(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("fcm-send"),
        sp.GetRequiredService<GoogleAccessTokenProvider>(),
        projectId,
        TimeSpan.FromSeconds(fcmOptions.RequestTimeoutSeconds)));
}
else
{
    builder.Services.AddScoped<IFcmSender, NoOpFcmSender>();
}

// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IDonorService, DonorService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();

builder.Services.AddScoped<DonorRankingCalculator>();
builder.Services.AddScoped<IMatchingDispatchAgentService, MatchingDispatchAgentService>();

builder.Services.AddScoped<IEligibilityRuleEngine, EligibilityRuleEngine>();

builder.Services.AddScoped<IRequestService, RequestService>();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IStaffInvitationService, StaffInvitationService>();

builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// =====================================================
// DATABASE
// =====================================================

var dataSourceBuilder =
    new Npgsql.NpgsqlDataSourceBuilder(
        builder.Configuration.GetConnectionString("Default"));

dataSourceBuilder.EnableDynamicJson();

var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
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
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!))
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
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description =
                "Enter your JWT token. Example: Bearer {your token}"
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Bootstraps the very first admin account. There's no other way to
    // get one: public registration always creates a donor (AuthService),
    // and staff accounts require an admin-issued invitation
    // (StaffInvitationService) — so without this, a fresh database has
    // no user who could ever issue that first invitation. Runs once per
    // startup, is a no-op once any admin exists, and only runs in
    // Development — production admin provisioning is a separate,
    // deliberately manual concern (this seeder is not safe to run
    // unattended against a real database).
    using var seedScope = app.Services.CreateScope();
    var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await seedContext.Users.AnyAsync(u => u.Role == BloodDonationNetwork.Domain.Entities.UserRole.Admin))
    {
        var adminEmail = (app.Configuration["Seed:AdminEmail"] ?? "admin@blooddonation.local").Trim().ToLowerInvariant();
        var adminPassword = app.Configuration["Seed:AdminPassword"] ?? "ChangeMe123!";

        seedContext.Users.Add(new BloodDonationNetwork.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role = BloodDonationNetwork.Domain.Entities.UserRole.Admin,
            FullName = "Admin",
            PhoneNumber = string.Empty,
            OrganizationId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await seedContext.SaveChangesAsync();

        app.Logger.LogWarning(
            "Seeded first admin account {Email} — set Seed:AdminEmail/Seed:AdminPassword in " +
            "appsettings.Development.json to override, and change this password after logging in.",
            adminEmail);
    }
}

app.UseHttpsRedirection();

app.UseMiddleware<BloodDonationNetwork.Api.Middleware.InternalSecretMiddleware>();

// Authentication must come before Authorization
app.UseAuthentication();

app.UseAuthorization();

// Map API controllers
app.MapControllers();

app.Run();