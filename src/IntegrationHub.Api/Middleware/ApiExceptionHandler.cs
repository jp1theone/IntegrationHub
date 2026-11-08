using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace IntegrationHub.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ExecutionException execution => execution.StatusCode,
            BusinessConflictException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status400BadRequest,
            DomainException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status >= 500) logger.LogError(exception, "Unhandled API error. CorrelationId={CorrelationId}", httpContext.TraceIdentifier);
        else logger.LogWarning("Request rejected with {StatusCode}: {ErrorMessage}. CorrelationId={CorrelationId}", status, exception.Message, httpContext.TraceIdentifier);
        var problem = new ProblemDetails { Status = status, Title = status >= 500 ? "An unexpected server error occurred." : exception.Message, Instance = httpContext.Request.Path };
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
