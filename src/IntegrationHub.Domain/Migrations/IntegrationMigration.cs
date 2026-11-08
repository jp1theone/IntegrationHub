using IntegrationHub.Domain.Common;

namespace IntegrationHub.Domain.Migrations;

public sealed class IntegrationMigration
{
    private IntegrationMigration() { }
    public IntegrationMigration(string migrationNumber, Guid clientId, Guid integrationId, string sourceProvider, string targetProvider, string? assignedToUserId, string? notes)
    {
        Id = Guid.NewGuid(); MigrationNumber = migrationNumber; ClientId = clientId; IntegrationId = integrationId;
        SourceProvider = sourceProvider; TargetProvider = targetProvider; AssignedToUserId = assignedToUserId;
        Notes = notes; Status = MigrationStatus.Planned; CreatedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; private set; }
    public string MigrationNumber { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public Guid IntegrationId { get; private set; }
    public string SourceProvider { get; private set; } = string.Empty;
    public string TargetProvider { get; private set; } = string.Empty;
    public MigrationStatus Status { get; private set; }
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? EndDate { get; private set; }
    public string? AssignedToUserId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<MigrationHistory> History { get; private set; } = new List<MigrationHistory>();
    public void ChangeStatus(MigrationStatus status, string? changedBy)
    {
        var now = DateTimeOffset.UtcNow; Status = status; UpdatedAt = now;
        if (status == MigrationStatus.InProgress) StartDate ??= now;
        if (status is MigrationStatus.Completed or MigrationStatus.Failed or MigrationStatus.Cancelled) EndDate = now;
        History.Add(new MigrationHistory(Id, status, changedBy, now));
    }
    public void Assign(string? userId) { AssignedToUserId = userId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void AddNotes(string? notes) { Notes = notes?.Trim(); UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class MigrationHistory
{
    private MigrationHistory() { }
    public MigrationHistory(Guid migrationId, MigrationStatus status, string? changedBy, DateTimeOffset changedAt)
    { Id = Guid.NewGuid(); IntegrationMigrationId = migrationId; Status = status; ChangedByUserId = changedBy; ChangedAt = changedAt; }
    public Guid Id { get; private set; }
    public Guid IntegrationMigrationId { get; private set; }
    public MigrationStatus Status { get; private set; }
    public string? ChangedByUserId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}
