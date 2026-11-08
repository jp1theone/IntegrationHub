using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;

namespace IntegrationHub.Application.Incidents;

public sealed record IncidentCommentDto(Guid Id, string AuthorUserId, string Content, DateTimeOffset CreatedAt);
public sealed record IncidentDto(Guid Id, string IncidentNumber, Guid IntegrationId, Guid? ExecutionId, string Title, string Description, IncidentSeverity Severity, IncidentStatus Status, string? ErrorCode, string? AssignedToUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ResolvedAt, IReadOnlyList<IncidentCommentDto> Comments);
public sealed record CreateIncidentRequest(Guid IntegrationId, Guid? ExecutionId, string Title, string Description, IncidentSeverity Severity, string? ErrorCode);

public interface IIncidentStore
{
    Task<PagedResult<IncidentDto>> SearchAsync(string? query, IncidentStatus? status, IncidentSeverity? severity, int page, int pageSize, CancellationToken cancellationToken);
    Task<IncidentDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<Incident?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IntegrationExistsAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Incident incident, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class IncidentUseCases(IIncidentStore incidents)
{
    public Task<PagedResult<IncidentDto>> SearchAsync(string? q, IncidentStatus? status, IncidentSeverity? severity, int page, int size, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, size);
        return incidents.SearchAsync(q?.Trim(), status, severity, normalized.Page, normalized.PageSize, ct);
    }
    public Task<IncidentDto?> GetAsync(Guid id, CancellationToken ct) => incidents.GetDtoAsync(id, ct);
    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct)
    {
        if (!await incidents.IntegrationExistsAsync(request.IntegrationId, ct)) throw new BusinessConflictException("Integration does not exist.");
        var number = $"INC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26];
        var incident = new Incident(number, request.IntegrationId, request.ExecutionId, request.Title.Trim(), request.Description.Trim(), request.Severity, request.ErrorCode?.Trim());
        await incidents.AddAsync(incident, ct);
        return (await incidents.GetDtoAsync(incident.Id, ct))!;
    }
    public async Task<bool> UpdateStatusAsync(Guid id, IncidentStatus status, CancellationToken ct)
    {
        var incident = await incidents.GetAsync(id, ct); if (incident is null) return false;
        incident.SetStatus(status); await incidents.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> AssignAsync(Guid id, string? userId, CancellationToken ct)
    {
        var incident = await incidents.GetAsync(id, ct); if (incident is null) return false;
        incident.Assign(userId); await incidents.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> SetSeverityAsync(Guid id, IncidentSeverity severity, CancellationToken ct)
    {
        var incident = await incidents.GetAsync(id, ct); if (incident is null) return false;
        incident.SetSeverity(severity); await incidents.SaveChangesAsync(ct); return true;
    }
    public async Task<IncidentDto?> AddCommentAsync(Guid id, string authorUserId, string content, CancellationToken ct)
    {
        var incident = await incidents.GetAsync(id, ct); if (incident is null) return null;
        incident.AddComment(new IncidentComment(id, authorUserId, content.Trim()));
        await incidents.SaveChangesAsync(ct);
        return await incidents.GetDtoAsync(id, ct);
    }
}
