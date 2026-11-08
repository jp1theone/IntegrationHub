using IntegrationHub.Domain.Common;

namespace IntegrationHub.Domain.Incidents;

public sealed class Incident
{
    private Incident() { }
    public Incident(string number, Guid integrationId, Guid? executionId, string title, string description, IncidentSeverity severity, string? errorCode)
    {
        Id = Guid.NewGuid(); IncidentNumber = number; IntegrationId = integrationId; ExecutionId = executionId;
        Title = title; Description = description; Severity = severity; ErrorCode = errorCode;
        Status = IncidentStatus.Open; CreatedAt = DateTimeOffset.UtcNow; UpdatedAt = CreatedAt;
    }
    public Guid Id { get; private set; }
    public string IncidentNumber { get; private set; } = string.Empty;
    public Guid IntegrationId { get; private set; }
    public Guid? ExecutionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IncidentSeverity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? AssignedToUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public ICollection<IncidentComment> Comments { get; private set; } = new List<IncidentComment>();
    public void Assign(string? userId) { AssignedToUserId = userId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void SetSeverity(IncidentSeverity severity) { Severity = severity; UpdatedAt = DateTimeOffset.UtcNow; }
    public void SetStatus(IncidentStatus status)
    {
        Status = status; UpdatedAt = DateTimeOffset.UtcNow;
        if (status is IncidentStatus.Resolved or IncidentStatus.Closed) ResolvedAt ??= UpdatedAt;
    }
    public void AddComment(IncidentComment comment) { Comments.Add(comment); UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class IncidentComment
{
    private IncidentComment() { }
    public IncidentComment(Guid incidentId, string authorUserId, string content)
    { Id = Guid.NewGuid(); IncidentId = incidentId; AuthorUserId = authorUserId; Content = content; CreatedAt = DateTimeOffset.UtcNow; }
    public Guid Id { get; private set; }
    public Guid IncidentId { get; private set; }
    public string AuthorUserId { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
