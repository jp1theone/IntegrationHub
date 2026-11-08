using System.Text.Json;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Webhooks;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class WebhooksController(WebhookUseCases webhooks, WebhookSignatureValidator signatures) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("webhooks")]
    [HttpPost("webhooks/{integrationCode}")]
    [RequestSizeLimit(262_144)]
    [Consumes("application/json")]
    public async Task<IActionResult> Receive(string integrationCode, CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > 262_144) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (!signatures.IsValid(payload, Request.Headers["X-Webhook-Signature"].FirstOrDefault()))
            return Unauthorized(new ProblemDetails { Title = "Webhook signature is invalid.", Status = StatusCodes.Status401Unauthorized });
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind != JsonValueKind.Object) return BadRequest(new ProblemDetails { Title = "Webhook payload must be a JSON object.", Status = 400 });
        }
        catch (JsonException)
        {
            return BadRequest(new ProblemDetails { Title = "Webhook payload is not valid JSON.", Status = 400 });
        }
        var eventId = Request.Headers["X-Event-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(eventId)) return BadRequest(new ProblemDetails { Title = "X-Event-ID is required for webhook deduplication.", Status = 400 });
        var result = await webhooks.ReceiveAsync(integrationCode, eventId, payload, cancellationToken);
        return Accepted($"/api/webhook-events/{result.Id}", result);
    }

    [Authorize(Policy = AuthorizationPolicies.ViewOperations)]
    [HttpGet("webhook-events")]
    public Task<IntegrationHub.Application.Common.PagedResult<WebhookEventDto>> Search([FromQuery] string? q,
        [FromQuery] WebhookEventStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => webhooks.SearchAsync(q, status, page, pageSize, cancellationToken);

    [Authorize(Policy = AuthorizationPolicies.ViewOperations)]
    [HttpGet("webhook-events/{id:guid}")]
    public async Task<ActionResult<WebhookEventDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await webhooks.GetAsync(id, cancellationToken) is { } item ? Ok(item) : NotFound();
}
