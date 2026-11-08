using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Migrations;
using IntegrationHub.Domain.Observability;
using IntegrationHub.Domain.Providers;
using IntegrationHub.Domain.Webhooks;
using IntegrationHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class IntegrationHubDbContext(DbContextOptions<IntegrationHubDbContext> options)
    : IdentityDbContext<IntegrationHubUser, IdentityRole, string>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<IntegrationExecution> Executions => Set<IntegrationExecution>();
    public DbSet<ExecutionAttempt> ExecutionAttempts => Set<ExecutionAttempt>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<IntegrationMigration> IntegrationMigrations => Set<IntegrationMigration>();
    public DbSet<MigrationHistory> MigrationHistory => Set<MigrationHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var payloadColumnType = Database.IsSqlite() ? "TEXT" : "nvarchar(max)";
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.HasIndex(x => x.Code).IsUnique(); entity.HasIndex(x => x.Email);
        });
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("Providers"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.BaseUrl).HasMaxLength(400);
            entity.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<Integration>(entity =>
        {
            entity.ToTable("Integrations"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(48).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne(x => x.Client).WithMany(x => x.Integrations).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Provider).WithMany(x => x.Integrations).HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ClientId, x.Status }); entity.HasIndex(x => new { x.ProviderId, x.Status });
        });
        modelBuilder.Entity<IntegrationExecution>(entity =>
        {
            entity.ToTable("IntegrationExecutions"); entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestPayload).HasColumnType(payloadColumnType).IsRequired();
            entity.Property(x => x.ResponsePayload).HasColumnType(payloadColumnType);
            entity.Property(x => x.ErrorCode).HasMaxLength(80); entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.HasOne(x => x.Integration).WithMany(x => x.Executions).HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.IntegrationId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(x => x.IntegrationId); entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.Status); entity.HasIndex(x => x.StartedAt);
            entity.HasIndex(x => new { x.Status, x.NextRetryAt });
        });
        modelBuilder.Entity<ExecutionAttempt>(entity =>
        {
            entity.ToTable("ExecutionAttempts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.ErrorCode).HasMaxLength(80); entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasOne<IntegrationExecution>().WithMany(x => x.Attempts).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ExecutionId, x.AttemptNumber }).IsUnique();
        });
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvents"); entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalEventId).HasMaxLength(200);
            entity.Property(x => x.Payload).HasColumnType(payloadColumnType).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(x => x.Integration).WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.IntegrationId, x.ExternalEventId }).IsUnique().HasFilter("[ExternalEventId] IS NOT NULL");
            entity.HasIndex(x => x.CorrelationId); entity.HasIndex(x => x.Status);
        });
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("Incidents"); entity.HasKey(x => x.Id);
            entity.Property(x => x.IncidentNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ErrorCode).HasMaxLength(80); entity.Property(x => x.AssignedToUserId).HasMaxLength(450);
            entity.HasOne<Integration>().WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntegrationExecution>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.IncidentNumber).IsUnique(); entity.HasIndex(x => x.Status); entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => new { x.IntegrationId, x.ErrorCode }).HasFilter("[Status] IN (0, 1)");
        });
        modelBuilder.Entity<IncidentComment>(entity =>
        {
            entity.ToTable("IncidentComments"); entity.HasKey(x => x.Id);
            entity.Property(x => x.AuthorUserId).HasMaxLength(450).IsRequired(); entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            entity.HasOne<Incident>().WithMany(x => x.Comments).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<IntegrationLog>(entity =>
        {
            entity.ToTable("IntegrationLogs"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired(); entity.Property(x => x.ErrorCode).HasMaxLength(80);
            entity.HasIndex(x => x.CorrelationId); entity.HasIndex(x => x.IntegrationId); entity.HasIndex(x => x.ProviderId);
            entity.HasIndex(x => new { x.Level, x.OccurredAt });
        });
        modelBuilder.Entity<IntegrationMigration>(entity =>
        {
            entity.ToTable("IntegrationMigrations"); entity.HasKey(x => x.Id);
            entity.Property(x => x.MigrationNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceProvider).HasMaxLength(160).IsRequired(); entity.Property(x => x.TargetProvider).HasMaxLength(160).IsRequired();
            entity.Property(x => x.AssignedToUserId).HasMaxLength(450); entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasOne<Integration>().WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MigrationNumber).IsUnique(); entity.HasIndex(x => x.Status);
        });
        modelBuilder.Entity<MigrationHistory>(entity =>
        {
            entity.ToTable("MigrationHistory"); entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangedByUserId).HasMaxLength(450);
            entity.HasOne<IntegrationMigration>().WithMany(x => x.History).HasForeignKey(x => x.IntegrationMigrationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs"); entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired(); entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120).IsRequired(); entity.Property(x => x.EntityId).HasMaxLength(120);
            entity.Property(x => x.CorrelationId).HasMaxLength(80); entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
        });
        modelBuilder.Entity<IntegrationHubUser>(entity => { entity.Property(x => x.FullName).HasMaxLength(160).IsRequired(); });
    }
}
