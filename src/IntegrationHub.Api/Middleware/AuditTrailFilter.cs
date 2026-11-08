using System.Security.Claims;
using IntegrationHub.Application.Executions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IntegrationHub.Api.Middleware;

public sealed class AuditTrailFilter(IOperationalWriter writer, ILogger<AuditTrailFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var request = context.HttpContext.Request;
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true) return;
        if (request.Method is not ("POST" or "PUT" or "PATCH" or "DELETE") || executed.HttpContext.Response.StatusCode >= 400) return;
        var actor = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return;
        var controller = context.RouteData.Values.GetValueOrDefault("controller")?.ToString() ?? "Unknown";
        var entityId = context.RouteData.Values.GetValueOrDefault("id")?.ToString();
        try
        {
            await writer.WriteAuditAsync(actor, request.Method, controller, entityId, context.HttpContext.TraceIdentifier, context.HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to persist audit event for {Controller} {Method} with correlation {CorrelationId}.", controller, request.Method, context.HttpContext.TraceIdentifier);
        }
    }
}
