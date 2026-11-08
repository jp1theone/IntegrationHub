using IntegrationHub.Application.Auth;
using System.Net.Http.Json;
using IntegrationHub.Infrastructure.Identity;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationHub.IntegrationTests;

public sealed class IntegrationHubApiFactory : WebApplicationFactory<Program>
{
    public const string TestPassword = "Test-Only-Password!42";
    private const string TestAdminEmail = "admin@integrationhub.test";
    private const string TestReadOnlyEmail = "readonly@integrationhub.test";
    private const string TestSigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    public const string TestWebhookSecret = "integrationhub-test-webhook-signing-secret";
    private readonly string testDatabase;
    private readonly SqliteConnection keeper;
    private readonly Dictionary<string, string?> previousEnvironment = new();
    private static readonly object InitializationLock = new();
    private bool databaseInitialized;

    public IntegrationHubApiFactory()
    {
        testDatabase = $"Data Source=IntegrationHubTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        keeper = new SqliteConnection(testDatabase);
        keeper.Open();
        SetEnvironment("ConnectionStrings__IntegrationHub", testDatabase);
        SetEnvironment("Database__Provider", "Sqlite");
        SetEnvironment("Database__ApplyMigrationsOnStartup", "false");
        SetEnvironment("Retry__WorkerEnabled", "false");
        SetEnvironment("Webhooks__WorkerEnabled", "false");
        SetEnvironment("RateLimiting__AuthPermitLimit", "100");
        SetEnvironment("Jwt__SigningKey", TestSigningKey);
        SetEnvironment("Jwt__Issuer", "IntegrationHub.Api");
        SetEnvironment("Jwt__Audience", "IntegrationHub.Web");
        SetEnvironment("Webhooks__SigningSecret", TestWebhookSecret);
    }

    private void SetEnvironment(string name, string value)
    {
        previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IntegrationHub"] = testDatabase,
            ["Database:Provider"] = "Sqlite",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["Retry:WorkerEnabled"] = "false",
            ["Webhooks:WorkerEnabled"] = "false",
            ["RateLimiting:AuthPermitLimit"] = "100",
            ["Jwt:SigningKey"] = TestSigningKey,
            ["Jwt:Issuer"] = "IntegrationHub.Api",
            ["Jwt:Audience"] = "IntegrationHub.Web",
            ["Webhooks:SigningSecret"] = TestWebhookSecret
        }));
    }

    public void InitializeDatabase()
    {
        lock (InitializationLock)
        {
            if (databaseInitialized) return;

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();
            db.Database.EnsureCreated();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IntegrationHubUser>>();
            foreach (var roleName in RoleNames.All)
                if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                    roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
            CreateUser(userManager, TestAdminEmail, "Test Administrator", RoleNames.Administrator);
            CreateUser(userManager, TestReadOnlyEmail, "Test Read Only", RoleNames.ReadOnly);
            databaseInitialized = true;
        }
    }

    private static void CreateUser(UserManager<IntegrationHubUser> users, string email, string fullName, string role)
    {
        var user = users.FindByEmailAsync(email).GetAwaiter().GetResult();
        if (user is null)
        {
            user = new IntegrationHubUser { UserName = email, Email = email, FullName = fullName, IsActive = true, EmailConfirmed = true };
            var created = users.CreateAsync(user, TestPassword).GetAwaiter().GetResult();
            if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
        }
        if (!users.IsInRoleAsync(user, role).GetAwaiter().GetResult())
            users.AddToRoleAsync(user, role).GetAwaiter().GetResult();
    }

    public HttpClient CreateAuthenticatedClient(string email = TestAdminEmail)
    {
        var client = CreateClient();
        var login = client.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword }).GetAwaiter().GetResult();
        if (!login.IsSuccessStatusCode) throw new InvalidOperationException("Test authentication failed.");
        using var document = System.Text.Json.JsonDocument.Parse(login.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", document.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            keeper.Dispose();
            foreach (var (name, value) in previousEnvironment) Environment.SetEnvironmentVariable(name, value);
        }
        base.Dispose(disposing);
    }
}
