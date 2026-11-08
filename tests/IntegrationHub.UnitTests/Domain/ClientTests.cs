using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;

namespace IntegrationHub.UnitTests.Domain;

public sealed class ClientTests
{
    [Fact]
    public void Constructor_normalizes_code_and_email()
    {
        var client = new Client(" acme-01 ", "Acme", "Acme S.A.", "OPS@EXAMPLE.TEST", null);
        Assert.Equal("ACME-01", client.Code);
        Assert.Equal("ops@example.test", client.Email);
        Assert.Equal(RecordStatus.Active, client.Status);
    }

    [Fact]
    public void Deactivated_client_is_inactive()
    {
        var client = new Client("ACME", "Acme", "Acme S.A.", "ops@example.test", null);
        client.SetActive(false);
        Assert.Equal(RecordStatus.Inactive, client.Status);
        Assert.NotNull(client.UpdatedAt);
    }

    [Fact]
    public void Blank_code_is_rejected() => Assert.Throws<DomainException>(() => new Client(" ", "Acme", "Acme S.A.", "ops@example.test", null));
}
