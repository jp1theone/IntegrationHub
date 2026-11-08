namespace IntegrationHub.Domain.Common;

public enum RecordStatus { Active, Inactive }
// Keep existing numeric values stable: the first three values are persisted in local databases.
public enum ProviderType { FudoMock, ToteatMock, GenericRestMock, Fudo, Toteat, GenericRest }
public enum IntegrationType { RestApi, Webhook, SdkSimulation }
public enum IntegrationStatus { Active, Inactive, Degraded, Error }
public enum IntegrationEnvironment { Development, Testing, Staging, Production }
public enum ProviderFailureMode { None, Timeout, Authentication, Validation, Unavailable, Http500 }
public enum ExecutionStatus { Pending, Processing, Succeeded, Failed, RetryScheduled, DeadLetter }
public enum FailureKind { None, Transient, Permanent }
public enum WebhookEventStatus { Queued, Processing, Processed, Failed, Duplicate }
public enum IncidentSeverity { Low, Medium, High, Critical }
public enum IncidentStatus { Open, InProgress, Resolved, Closed }
public enum MigrationStatus { Planned, InProgress, Completed, Failed, Cancelled }
public enum OperationalLogLevel { Information, Warning, Error, Critical }
