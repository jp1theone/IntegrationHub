using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationHub.IntegrationTests;

public sealed class ApiSmokeTests(IntegrationHubApiFactory factory) : IClassFixture<IntegrationHubApiFactory>
{
    private readonly HttpClient client = Initialize(factory);

    private static HttpClient Initialize(IntegrationHubApiFactory factory)
    {
        factory.InitializeDatabase();
        return factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous_and_reports_healthy()
    {
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operational_endpoints_require_authentication()
    {
        var response = await client.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_can_login_and_create_a_client()
    {
        using var authenticated = factory.CreateAuthenticatedClient();
        var response = await authenticated.PostAsJsonAsync("/api/clients", new
        {
            code = "TEST-CLIENT",
            name = "Test Client",
            legalName = "Test Client S.A.",
            email = "ops@example.test",
            phone = ""
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TEST-CLIENT", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Read_only_role_cannot_create_a_client()
    {
        using var authenticated = factory.CreateAuthenticatedClient("readonly@integrationhub.test");
        var response = await authenticated.PostAsJsonAsync("/api/clients", new
        {
            code = "READONLY-CLIENT",
            name = "Read Only Client",
            legalName = "Read Only Client S.A.",
            email = "read@example.test",
            phone = ""
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_login_is_rejected_without_exposing_details()
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@integrationhub.test", password = "incorrect" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Test-Only-Password", body);
    }
}
