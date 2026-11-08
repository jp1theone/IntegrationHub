using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Providers;

namespace IntegrationHub.Domain.Integrations;

public sealed class Integration
{
    private Integration() { }

    public Integration(Guid clientId, Guid providerId, string code, string name, string description, IntegrationType type, IntegrationEnvironment environment)
    {
        if (clientId == Guid.Empty || providerId == Guid.Empty) throw new DomainException("Client and provider are required.");
        Id = Guid.NewGuid();
        ClientId = clientId;
        ProviderId = providerId;
        Code = string.IsNullOrWhiteSpace(code) ? throw new DomainException("Integration code is required.") : code.Trim().ToUpperInvariant();
        Name = string.IsNullOrWhiteSpace(name) ? throw new DomainException("Integration name is required.") : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        Environment = environment;
        Status = IntegrationStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid ProviderId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IntegrationType Type { get; private set; }
    public IntegrationStatus Status { get; private set; }
    public IntegrationEnvironment Environment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Client? Client { get; private set; }
    public Provider? Provider { get; private set; }
    public ICollection<IntegrationExecution> Executions { get; private set; } = new List<IntegrationExecution>();

    public void Update(string name, string description, IntegrationType type, IntegrationEnvironment environment)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new DomainException("Integration name is required.") : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        Environment = environment;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(IntegrationStatus status) { Status = status; UpdatedAt = DateTimeOffset.UtcNow; }
    public bool CanExecute => Status == IntegrationStatus.Active && Client?.Status == RecordStatus.Active && Provider?.Status == RecordStatus.Active;
}
