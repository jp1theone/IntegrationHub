using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IntegrationHub.Api.Middleware;
using IntegrationHub.Api.Security;
using IntegrationHub.Application;
using IntegrationHub.Application.Auth;
using IntegrationHub.Application.Clients;
using IntegrationHub.Application.Integrations;
using IntegrationHub.Application.Providers;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Application.Migrations;
using IntegrationHub.Application.Webhooks;
using IntegrationHub.Application.Users;
using FluentValidation;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Identity;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, logger) => logger.MinimumLevel.Information().Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(new IntegrationHub.Application.Executions.RetryPolicyOptions(
    builder.Configuration.GetValue("Retry:MaxAttempts", 3),
    builder.Configuration.GetValue("Retry:InitialDelaySeconds", 5),
    builder.Configuration.GetValue("Retry:BackoffMultiplier", 2d)));
builder.Services.AddSingleton(new IntegrationHub.Application.Reporting.SlaPolicyOptions(
    builder.Configuration.GetValue("Sla:WindowDays", 30),
    builder.Configuration.GetValue("Sla:AvailabilityTargetPercent", 99m),
    builder.Configuration.GetValue("Sla:MaximumAverageDurationMs", 2000)));
builder.Services.AddScoped<IValidator<CreateClientRequest>, CreateClientValidator>();
builder.Services.AddScoped<IValidator<UpdateClientRequest>, UpdateClientValidator>();
builder.Services.AddScoped<IValidator<CreateProviderRequest>, CreateProviderValidator>();
builder.Services.AddScoped<IValidator<UpdateProviderRequest>, UpdateProviderValidator>();
builder.Services.AddScoped<IValidator<CreateIntegrationRequest>, CreateIntegrationValidator>();
builder.Services.AddScoped<IValidator<UpdateIntegrationRequest>, UpdateIntegrationValidator>();
builder.Services.AddScoped<IValidator<LoginCredentials>, LoginCredentialsValidator>();
builder.Services.AddScoped<IValidator<CreateIncidentRequest>, CreateIncidentValidator>();
builder.Services.AddScoped<IValidator<AddIncidentCommentRequest>, AddIncidentCommentValidator>();
builder.Services.AddScoped<IValidator<CreateMigrationRequest>, CreateMigrationValidator>();
builder.Services.AddScoped<IValidator<UpdateMigrationRequest>, UpdateMigrationValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserValidator>();
builder.Services.AddSingleton<WebhookSignatureValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<AuditTrailFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<AuditTrailFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "IntegrationHub API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Use the access token issued by POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddCors(options => options.AddPolicy("WebClient", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 5), Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0, AutoReplenishment = true
        }));
    options.AddPolicy("webhooks", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:WebhookPermitLimit", 60), Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0, AutoReplenishment = true
        }));
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddIntegrationHubPolicies();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey)) throw new InvalidOperationException("Jwt:SigningKey is missing. Set it with dotnet user-secrets before running the API.");
byte[] jwtKeyBytes;
try { jwtKeyBytes = Convert.FromBase64String(jwtSigningKey); }
catch (FormatException exception) { throw new InvalidOperationException("Jwt:SigningKey must be base64 encoded.", exception); }
if (jwtKeyBytes.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must contain at least 256 bits.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IntegrationHub.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IntegrationHub.Web";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwtIssuer, ValidateAudience = true, ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<IntegrationHubUser>>();
            var user = userId is null ? null : await manager.FindByIdAsync(userId);
            if (user is null || !user.IsActive) context.Fail("User account is inactive or no longer exists.");
        }
    };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup") && app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("WebClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "IntegrationHub.Api", timestampUtc = DateTimeOffset.UtcNow })).AllowAnonymous();
app.Run();

public partial class Program { }
