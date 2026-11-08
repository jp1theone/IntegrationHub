using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Observability;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfOperationalWriter(IntegrationHubDbContext db) : IOperationalWriter
{
    public async Task WriteAuditAsync(string actorUserId, string action, string entityType, string? entityId, string? correlationId, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog(actorUserId, action, entityType, entityId, correlationId));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteLogAsync(OperationalLogLevel level, string message, Guid? correlationId, Guid? integrationId, Guid? executionId, Guid? providerId, string? errorCode, CancellationToken cancellationToken)
    {
        db.IntegrationLogs.Add(new IntegrationLog(level, message, correlationId, integrationId, executionId, providerId, errorCode));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureIncidentAsync(Guid integrationId, Guid executionId, string errorCode, string message, IncidentSeverity severity, CancellationToken cancellationToken)
    {
        var duplicate = await db.Incidents.AnyAsync(x => x.IntegrationId == integrationId && x.ErrorCode == errorCode && (x.Status == IncidentStatus.Open || x.Status == IncidentStatus.InProgress), cancellationToken);
        if (duplicate) return;
        var incident = new Incident($"INC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26], integrationId, executionId,
            $"Integration failure: {errorCode}", message, severity, errorCode);
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);
    }
}
