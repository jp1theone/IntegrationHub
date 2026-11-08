using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfExecutionStore(IntegrationHubDbContext db) : IExecutionStore, IIntegrationCatalog
{
    public async Task<PagedResult<ExecutionDto>> SearchAsync(string? query, Guid? integrationId, ExecutionStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct)
    {
        var executions = db.Executions.AsNoTracking().Include(x => x.Integration).ThenInclude(x => x!.Provider).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) executions = executions.Where(x => x.CorrelationId.ToString().Contains(query) || (x.ErrorCode != null && EF.Functions.Like(x.ErrorCode, $"%{query}%")) || x.Integration!.Code.Contains(query));
        if (integrationId.HasValue) executions = executions.Where(x => x.IntegrationId == integrationId.Value);
        if (status.HasValue) executions = executions.Where(x => x.Status == status.Value);
        if (from.HasValue) executions = executions.Where(x => x.StartedAt >= from.Value);
        if (to.HasValue) executions = executions.Where(x => x.StartedAt <= to.Value);
        var total = await executions.CountAsync(ct);
        var rows = await executions.OrderByDescending(x => x.StartedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ExecutionDto(x.Id, x.IntegrationId, x.Integration!.Code, x.Integration.Provider!.Name, x.CorrelationId,
                x.IdempotencyKey, x.RequestPayload, x.ResponsePayload, x.Status, x.AttemptNumber, x.StartedAt, x.CompletedAt,
                x.DurationMs, x.HttpStatusCode, x.ErrorCode, x.ErrorMessage, x.NextRetryAt,
                x.Attempts.OrderBy(a => a.AttemptNumber).Select(a => new ExecutionAttemptDto(a.AttemptNumber, a.StartedAt, a.CompletedAt, a.DurationMs, a.Succeeded, a.HttpStatusCode, a.ErrorCode, a.ErrorMessage)).ToList())).ToListAsync(ct);
        return new PagedResult<ExecutionDto>(rows, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public Task<ExecutionDto?> GetDtoAsync(Guid executionId, CancellationToken ct) => db.Executions.AsNoTracking().Where(x => x.Id == executionId)
        .Select(x => new ExecutionDto(x.Id, x.IntegrationId, x.Integration!.Code, x.Integration.Provider!.Name, x.CorrelationId,
            x.IdempotencyKey, x.RequestPayload, x.ResponsePayload, x.Status, x.AttemptNumber, x.StartedAt, x.CompletedAt,
            x.DurationMs, x.HttpStatusCode, x.ErrorCode, x.ErrorMessage, x.NextRetryAt,
            x.Attempts.OrderBy(a => a.AttemptNumber).Select(a => new ExecutionAttemptDto(a.AttemptNumber, a.StartedAt, a.CompletedAt, a.DurationMs, a.Succeeded, a.HttpStatusCode, a.ErrorCode, a.ErrorMessage)).ToList())).SingleOrDefaultAsync(ct);

    public Task<Integration?> GetIntegrationAsync(Guid id, CancellationToken cancellationToken) =>
        db.Integrations.Include(x => x.Client).Include(x => x.Provider).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Integration?> GetIntegrationByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Integrations.Include(x => x.Client).Include(x => x.Provider).SingleOrDefaultAsync(x => x.Code == code.ToUpperInvariant(), cancellationToken);

    public Task<IntegrationExecution?> FindSuccessfulByIdempotencyAsync(Guid integrationId, string key, CancellationToken cancellationToken) =>
        db.Executions.Include(x => x.Attempts).SingleOrDefaultAsync(x => x.IntegrationId == integrationId && x.IdempotencyKey == key && x.Status == ExecutionStatus.Succeeded, cancellationToken);

    public Task<IntegrationExecution?> FindByIdAsync(Guid executionId, CancellationToken cancellationToken) =>
        db.Executions.Include(x => x.Attempts).SingleOrDefaultAsync(x => x.Id == executionId, cancellationToken);

    public async Task<bool> TryCreateAsync(IntegrationExecution execution, CancellationToken cancellationToken)
    {
        db.Executions.Add(execution);
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException)
        {
            db.Entry(execution).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveAsync(IntegrationExecution execution, CancellationToken cancellationToken)
    {
        // Execution aggregates use application-assigned GUID keys. When a new attempt is
        // appended to an already-tracked execution, EF can infer Modified from that key;
        // explicitly mark detached attempts as new rows before saving the aggregate.
        foreach (var attempt in execution.Attempts)
            if (db.Entry(attempt).State == EntityState.Detached)
                db.ExecutionAttempts.Add(attempt);

        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetDueRetryIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await db.Executions.AsNoTracking().Where(x => x.Status == ExecutionStatus.RetryScheduled && x.NextRetryAt <= now)
            .OrderBy(x => x.NextRetryAt).Select(x => x.Id).Take(take).ToListAsync(cancellationToken);

    public async Task<bool> TryClaimRetryAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await db.Executions.Where(x => x.Id == executionId && x.Status == ExecutionStatus.RetryScheduled && x.NextRetryAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, ExecutionStatus.Processing).SetProperty(x => x.NextRetryAt, (DateTimeOffset?)null), cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryClaimManualRetryAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var affected = await db.Executions.Where(x => x.Id == executionId && (x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.DeadLetter))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, ExecutionStatus.Processing).SetProperty(x => x.NextRetryAt, (DateTimeOffset?)null), cancellationToken);
        if (affected != 1) return false;
        var tracked = db.ChangeTracker.Entries<IntegrationExecution>().FirstOrDefault(entry => entry.Entity.Id == executionId);
        if (tracked is not null) await tracked.ReloadAsync(cancellationToken);
        return true;
    }
}
