using System.Diagnostics;
using Serilog.Context;

namespace IntegrationHub.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var supplied) && Guid.TryParse(supplied, out var parsed)
            ? parsed.ToString("D") : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("D");
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId)) await next(context);
    }
}
