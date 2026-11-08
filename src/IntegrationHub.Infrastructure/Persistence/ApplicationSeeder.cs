using IntegrationHub.Application.Auth;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Migrations;
using IntegrationHub.Domain.Providers;
using IntegrationHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class ApplicationSeeder(
    IntegrationHubDbContext db,
    RoleManager<IdentityRole> roles,
    UserManager<IntegrationHubUser> users,
    IConfiguration configuration,
    ILogger<ApplicationSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleNames.All)
            if (!await roles.RoleExistsAsync(roleName))
                Ensure(await roles.CreateAsync(new IdentityRole(roleName)), $"create role {roleName}");

        var password = configuration["Seed:DemoPassword"];
        if (!string.IsNullOrWhiteSpace(password)) await SeedUsersAsync(password);
        else logger.LogWarning("Demo users were not seeded. Set Seed:DemoPassword through user-secrets to enable local demo accounts.");

        if (await db.Clients.AnyAsync(cancellationToken)) return;
        var clients = Enumerable.Range(1, 5).Select(i => new Client($"CLIENT-{i:000}", $"Fictional Client {i}", $"Fictional Client {i} S.A.C.", $"ops{i}@example.test", $"+51 900 000 {i:000}")).ToArray();
        var providerModes = new[] { ProviderFailureMode.None, ProviderFailureMode.Timeout, ProviderFailureMode.Authentication, ProviderFailureMode.Validation, ProviderFailureMode.Unavailable };
        var providerTypes = new[] { ProviderType.FudoMock, ProviderType.ToteatMock, ProviderType.GenericRestMock, ProviderType.FudoMock, ProviderType.ToteatMock };
        var providerNames = new[] { "FUDO Mock", "TOTEAT Mock", "Generic REST Mock", "FUDO Sandbox Mock", "TOTEAT Sandbox Mock" };
        var providers = Enumerable.Range(1, 5).Select(i =>
        {
            var provider = new Provider($"MOCK-{i:000}", providerNames[i - 1], "Fictional deterministic provider for local integration scenarios.", providerTypes[i - 1]);
            provider.Configure(provider.Name, provider.Description, 5, providerModes[i - 1]);
            return provider;
        }).ToArray();
        var integrations = Enumerable.Range(1, 10).Select(i => new Integration(
            clients[(i - 1) % clients.Length].Id,
            providers[(i - 1) % providers.Length].Id,
            $"INT-{i:000}", $"Integration {i:00}", "Seeded fictional integration.",
            i % 3 == 0 ? IntegrationType.Webhook : IntegrationType.RestApi,
            IntegrationEnvironment.Development)).ToArray();
        db.Clients.AddRange(clients); db.Providers.AddRange(providers); db.Integrations.AddRange(integrations);
        await db.SaveChangesAsync(cancellationToken);

        var executions = new List<IntegrationExecution>();
        for (var i = 1; i <= 30; i++)
        {
            var integration = integrations[(i - 1) % integrations.Length];
            var execution = new IntegrationExecution(integration.Id, "{\"sample\":true}", null);
            execution.BeginAttempt(DateTimeOffset.UtcNow.AddMinutes(-i));
            if (i % 3 == 0) execution.CompleteSuccess("{\"outcome\":\"accepted\",\"simulated\":true}", 200, DateTimeOffset.UtcNow.AddMinutes(-i).AddMilliseconds(120));
            else
            {
                execution.CompleteFailure("PROVIDER_UNAVAILABLE", "Seeded simulated provider failure.", 503, FailureKind.Transient, DateTimeOffset.UtcNow.AddMinutes(-i).AddMilliseconds(180));
                if (i % 2 == 0) execution.ScheduleRetry(DateTimeOffset.UtcNow.AddMinutes(10));
                else execution.MarkDeadLetter(DateTimeOffset.UtcNow.AddMinutes(-i).AddMilliseconds(180));
            }
            var attempt = new ExecutionAttempt(execution.Id, 1, execution.StartedAt);
            attempt.Finish(execution.Status == ExecutionStatus.Succeeded, execution.HttpStatusCode, execution.ErrorCode, execution.ErrorMessage, execution.CompletedAt ?? DateTimeOffset.UtcNow);
            execution.Attempts.Add(attempt);
            executions.Add(execution);
        }
        db.Executions.AddRange(executions);
        await db.SaveChangesAsync(cancellationToken);

        for (var i = 1; i <= 5; i++)
            db.Incidents.Add(new Incident($"INC-SEED-{i:000}", integrations[i - 1].Id, executions[(i * 2) - 1].Id,
                $"Simulated incident {i}", "Fictional seeded incident for dashboard and workflow demonstrations.",
                (IncidentSeverity)((i - 1) % 4), "PROVIDER_UNAVAILABLE"));

        for (var i = 1; i <= 3; i++)
        {
            var migration = new IntegrationMigration($"MIG-SEED-{i:000}", clients[i - 1].Id, integrations[i - 1].Id,
                "Fictional Source Provider", "Fictional Target Provider", null, "Seeded simulated migration workflow.");
            migration.ChangeStatus(i == 1 ? MigrationStatus.InProgress : MigrationStatus.Planned, "seed");
            db.IntegrationMigrations.Add(migration);
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded fictional demo data: {Clients} clients, {Providers} providers, {Integrations} integrations, {Executions} executions, 5 incidents, and 3 migrations.", clients.Length, providers.Length, integrations.Length, executions.Count);
    }

    private async Task SeedUsersAsync(string password)
    {
        var samples = new[]
        {
            ("admin@integrationhub.local", "Demo Administrator", RoleNames.Administrator),
            ("integrations@integrationhub.local", "Demo Integration Analyst", RoleNames.IntegrationAnalyst),
            ("support@integrationhub.local", "Demo Support Analyst", RoleNames.SupportAnalyst),
            ("readonly@integrationhub.local", "Demo Read Only", RoleNames.ReadOnly)
        };
        foreach (var sample in samples)
        {
            var user = await users.FindByEmailAsync(sample.Item1);
            if (user is null)
            {
                user = new IntegrationHubUser { UserName = sample.Item1, Email = sample.Item1, FullName = sample.Item2, EmailConfirmed = true, IsActive = true };
                Ensure(await users.CreateAsync(user, password), "create demo user");
            }
            if (!await users.IsInRoleAsync(user, sample.Item3))
                Ensure(await users.AddToRoleAsync(user, sample.Item3), "assign demo role");
        }
    }

    private static void Ensure(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"Could not {operation}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
