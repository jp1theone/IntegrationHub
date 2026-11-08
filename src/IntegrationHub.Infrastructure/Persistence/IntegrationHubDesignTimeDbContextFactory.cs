using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class IntegrationHubDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IntegrationHubDbContext>
{
    public IntegrationHubDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IntegrationHub");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=localhost;Database=IntegrationHubDesignTime;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<IntegrationHubDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;
        return new IntegrationHubDbContext(options);
    }
}
