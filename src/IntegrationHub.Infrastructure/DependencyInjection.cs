using IntegrationHub.Application.Common;
using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Clients;
using IntegrationHub.Application.Providers;
using IntegrationHub.Application.Integrations;
using IntegrationHub.Application.Auth;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Application.Migrations;
using IntegrationHub.Application.Reporting;
using IntegrationHub.Application.Webhooks;
using IntegrationHub.Application.Users;
using IntegrationHub.Application.Observability;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Infrastructure.Providers;
using IntegrationHub.Infrastructure.Background;
using IntegrationHub.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IntegrationHub");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:IntegrationHub is required. Configure it with user-secrets or environment variables.");
        if (string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
            services.AddDbContext<IntegrationHubDbContext>(options => options.UseSqlite(connectionString));
        else
            services.AddDbContext<IntegrationHubDbContext>(options => options.UseSqlServer(connectionString,
                sql => sql.EnableRetryOnFailure(3)));
        services.AddIdentityCore<IntegrationHubUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>().AddEntityFrameworkStores<IntegrationHubDbContext>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<EfExecutionStore>();
        services.AddScoped<IExecutionStore>(sp => sp.GetRequiredService<EfExecutionStore>());
        services.AddScoped<IIntegrationCatalog>(sp => sp.GetRequiredService<EfExecutionStore>());
        services.AddScoped<IClientStore, EfClientStore>();
        services.AddScoped<IProviderStore, EfProviderStore>();
        services.AddScoped<IIntegrationStore, EfIntegrationStore>();
        services.AddScoped<IIncidentStore, EfIncidentStore>();
        services.AddScoped<IMigrationStore, EfMigrationStore>();
        services.AddScoped<IReportingStore, EfReportingStore>();
        services.AddScoped<IWebhookEventStore, EfWebhookEventStore>();
        services.AddScoped<IUserManagementStore, EfIdentityUserManagementStore>();
        services.AddScoped<ILogQueryStore, EfLogQueryStore>();
        services.AddScoped<IOperationalWriter, EfOperationalWriter>();
        services.AddScoped<ApplicationSeeder>();
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<IntegrationRuntimeOptions>(configuration.GetSection("IntegrationRuntime"));
        services.AddSingleton<DeterministicMockIntegrationProvider>();
        services.AddScoped<IIntegrationProvider, ModeAwareIntegrationProvider>();
        if (configuration.GetValue("Retry:WorkerEnabled", true)) services.AddHostedService<ScheduledRetryService>();
        if (configuration.GetValue("Webhooks:WorkerEnabled", true)) services.AddHostedService<WebhookProcessingService>();
        return services;
    }
}
