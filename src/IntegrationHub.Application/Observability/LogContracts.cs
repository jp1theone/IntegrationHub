using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;

namespace IntegrationHub.Application.Observability;

public sealed record IntegrationLogDto(Guid Id, OperationalLogLevel Level, string Message, Guid? CorrelationId, Guid? IntegrationId, Guid? ExecutionId, Guid? ProviderId, string? ErrorCode, DateTimeOffset OccurredAt);

public interface ILogQueryStore
{
    Task<PagedResult<IntegrationLogDto>> SearchAsync(string? query, Guid? correlationId, Guid? integrationId, Guid? providerId,
        Guid? executionId, OperationalLogLevel? level, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class LogQueryUseCases(ILogQueryStore logs)
{
    public Task<PagedResult<IntegrationLogDto>> SearchAsync(string? q, Guid? correlationId, Guid? integrationId, Guid? providerId,
        Guid? executionId, OperationalLogLevel? level, DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, size);
        return logs.SearchAsync(q?.Trim(), correlationId, integrationId, providerId, executionId, level, from, to, normalized.Page, normalized.PageSize, ct);
    }
}
