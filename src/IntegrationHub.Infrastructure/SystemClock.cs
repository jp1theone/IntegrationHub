using IntegrationHub.Application.Common;

namespace IntegrationHub.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
