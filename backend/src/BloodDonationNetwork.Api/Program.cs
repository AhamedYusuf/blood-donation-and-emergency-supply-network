using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Infrastructure.ExternalClients;
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

// =====================================================
// EXTERNAL CLIENTS
// =====================================================

builder.Services.AddHttpClient<IGeocodingClient, NominatimClient>(c =>
    c.DefaultRequestHeaders.Add(
        "User-Agent",
        "BloodDonationNetwork/1.0"));

// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<IDonorService, DonorService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();

builder.Services.AddScoped<DonorRankingCalculator>();
builder.Services.AddScoped<IMatchingDispatchAgentService, MatchingDispatchAgentService>();

builder.Services.AddScoped<IEligibilityRuleEngine, EligibilityRuleEngine>();

builder.Services.AddScoped<IRequestService, RequestService>();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddScoped<IAuthService, AuthService>();

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
}

app.UseHttpsRedirection();

app.UseMiddleware<BloodDonationNetwork.Api.Middleware.InternalSecretMiddleware>();

// Authentication must come before Authorization
app.UseAuthentication();

app.UseAuthorization();

// Map API controllers
app.MapControllers();

app.Run();