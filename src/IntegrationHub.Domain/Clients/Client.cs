using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;

namespace IntegrationHub.Domain.Clients;

public sealed class Client
{
    private Client() { }

    public Client(string code, string name, string legalName, string email, string? phone)
    {
        Id = Guid.NewGuid();
        Code = NormalizeCode(code);
        Name = Required(name, nameof(name));
        LegalName = Required(legalName, nameof(legalName));
        Email = Required(email, nameof(email)).Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Status = RecordStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public RecordStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public ICollection<Integration> Integrations { get; private set; } = new List<Integration>();

    public void Update(string name, string legalName, string email, string? phone)
    {
        Name = Required(name, nameof(name));
        LegalName = Required(legalName, nameof(legalName));
        Email = Required(email, nameof(email)).Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active)
    {
        Status = active ? RecordStatus.Active : RecordStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeCode(string value) => Required(value, nameof(value)).Trim().ToUpperInvariant();
    private static string Required(string value, string field) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainException($"{field} is required.")
        : value.Trim();
}
