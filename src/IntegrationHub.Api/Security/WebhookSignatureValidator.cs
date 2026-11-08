using System.Security.Cryptography;
using System.Text;

namespace IntegrationHub.Api.Security;

public sealed class WebhookSignatureValidator(IConfiguration configuration)
{
    public bool IsValid(string payload, string? suppliedSignature)
    {
        var secret = configuration["Webhooks:SigningSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(suppliedSignature)) return false;
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSignature.Trim());
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
