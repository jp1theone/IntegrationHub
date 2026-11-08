using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.IntegrationTests;

public sealed class OperationalFlowTests(IntegrationHubApiFactory factory) : IClassFixture<IntegrationHubApiFactory>
{
    [Fact]
    public async Task Integration_execution_is_persisted_and_successful_idempotency_key_is_replayed()
    {
        factory.InitializeDatabase();
        var integration = await CreateIntegrationAsync(IntegrationType.RestApi, ProviderFailureMode.None);
        using var authenticated = factory.CreateAuthenticatedClient();
        var path = $"/api/integrations/{integration.Id}/execute";
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { payload = new { reference = "test-order" } }) };
        request.Headers.Add("Idempotency-Key", "test-order-" + Guid.NewGuid().ToString("N"));

        var first = await authenticated.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var id = firstBody.RootElement.GetProperty("id").GetGuid();
        var idempotency = request.Headers.GetValues("Idempotency-Key").Single();
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { payload = new { reference = "different-payload-is-not-reprocessed" } }) };
        replayRequest.Headers.Add("Idempotency-Key", idempotency);

        var replay = await authenticated.SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(id, replayBody.RootElement.GetProperty("id").GetGuid());
        Assert.True(replayBody.RootElement.GetProperty("isReplay").GetBoolean());
    }

    [Fact]
    public async Task Signed_webhook_is_queued_and_duplicate_event_is_deduplicated()
    {
        factory.InitializeDatabase();
        var integration = await CreateIntegrationAsync(IntegrationType.Webhook, ProviderFailureMode.None);
        var body = "{\"event\":\"order.updated\",\"reference\":\"test-1\"}";
        var eventId = "event-" + Guid.NewGuid().ToString("N");

        var first = await SendWebhookAsync(integration.Code, eventId, body);
        var duplicate = await SendWebhookAsync(integration.Code, eventId, body);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        using var duplicateBody = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.True(duplicateBody.RootElement.GetProperty("duplicate").GetBoolean());
    }

    [Fact]
    public async Task Invalid_webhook_signature_is_rejected()
    {
        factory.InitializeDatabase();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/NO-MATTER") { Content = JsonContent.Create(new { eventName = "test" }) };
        request.Headers.Add("X-Webhook-Signature", "sha256=invalid");
        request.Headers.Add("X-Event-ID", Guid.NewGuid().ToString("N"));

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Integration> CreateIntegrationAsync(IntegrationType type, ProviderFailureMode mode)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var client = new Client($"C-{suffix}", $"Test Client {suffix}", $"Test Legal {suffix}", $"{suffix}@example.test", null);
        var provider = new Provider($"P-{suffix}", $"Test Provider {suffix}", "Test adapter", ProviderType.GenericRestMock);
        provider.Configure(provider.Name, provider.Description, 5, mode);
        var integration = new Integration(client.Id, provider.Id, $"I-{suffix}", $"Test Integration {suffix}", "Integration test record", type, IntegrationEnvironment.Testing);
        db.AddRange(client, provider, integration);
        await db.SaveChangesAsync();
        return integration;
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string integrationCode, string eventId, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/{integrationCode}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Event-ID", eventId);
        request.Headers.Add("X-Webhook-Signature", Sign(body));
        return await factory.CreateClient().SendAsync(request);
    }

    private static string Sign(string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(IntegrationHubApiFactory.TestWebhookSecret), Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
