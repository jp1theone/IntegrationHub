using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;

namespace IntegrationHub.Domain.Providers;

public sealed class Provider
{
    private Provider() { }

    public Provider(string code, string name, string description, ProviderType type, int timeoutSeconds = 30, string? baseUrl = null)
    {
        if (timeoutSeconds is < 1 or > 300) throw new DomainException("Timeout must be between 1 and 300 seconds.");
        Id = Guid.NewGuid();
        Code = string.IsNullOrWhiteSpace(code) ? throw new DomainException("Provider code is required.") : code.Trim().ToUpperInvariant();
        Name = string.IsNullOrWhiteSpace(name) ? throw new DomainException("Provider name is required.") : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        BaseUrl = NormalizeBaseUrl(baseUrl);
        TimeoutSeconds = timeoutSeconds;
        Status = RecordStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ProviderType Type { get; private set; }
    public string? BaseUrl { get; private set; }
    public RecordStatus Status { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public ProviderFailureMode FailureMode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public ICollection<Integration> Integrations { get; private set; } = new List<Integration>();

    public void Configure(string name, string description, int timeoutSeconds, ProviderFailureMode failureMode, string? baseUrl = null)
    {
        if (timeoutSeconds is < 1 or > 300) throw new DomainException("Timeout must be between 1 and 300 seconds.");
        Name = string.IsNullOrWhiteSpace(name) ? throw new DomainException("Provider name is required.") : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        TimeoutSeconds = timeoutSeconds;
        FailureMode = failureMode;
        BaseUrl = NormalizeBaseUrl(baseUrl);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active)
    {
        Status = active ? RecordStatus.Active : RecordStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new DomainException("Provider BaseUrl must be an absolute HTTP or HTTPS URL.");
        return normalized;
    }
}
