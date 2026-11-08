using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Clients;
using IntegrationHub.Application.Providers;
using IntegrationHub.Application.Integrations;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Application.Migrations;
using IntegrationHub.Application.Reporting;
using IntegrationHub.Application.Webhooks;
using IntegrationHub.Application.Users;
using IntegrationHub.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ExecuteIntegrationUseCase>();
        services.AddScoped<ClientUseCases>();
        services.AddScoped<ProviderUseCases>();
        services.AddScoped<IntegrationUseCases>();
        services.AddScoped<IncidentUseCases>();
        services.AddScoped<MigrationUseCases>();
        services.AddScoped<ReportingUseCases>();
        services.AddScoped<WebhookUseCases>();
        services.AddScoped<UserManagementUseCases>();
        services.AddScoped<LogQueryUseCases>();
        return services;
    }
}
