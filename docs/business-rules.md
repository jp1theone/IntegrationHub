# Business rules

- Client, provider, and integration codes are normalized and unique. Email values are normalized; request validation applies email format and required-field rules.
- An integration may execute only when it, its client, and its provider are active. Integration codes must be unique and both foreign keys must exist.
- Executions require a JSON object payload and receive correlation IDs. A successful idempotency key is replayed rather than executing again; a unique filtered database index and atomic creation guard concurrent duplicates.
- Every provider attempt is persisted with start/completion, outcome, duration, status and error information. Retry does not overwrite this history.
- Only transient failures are automatically retried. Backoff is `InitialDelaySeconds × BackoffMultiplier^(attempt-1)`, bounded by configured `MaxAttempts`. Permanent validation/authentication errors do not auto-retry. Exhausted retries reach dead-letter state.
- Scheduled and manual retry paths claim records atomically to prevent concurrent duplicate work. Manual retry requires the RetryExecution policy.
- Mock modes are deterministic: success, timeout, authentication rejection, validation rejection, unavailable, and simulated HTTP 500. No provider call contacts an external system.
- Webhook intake validates integration, status, JSON object, required event ID and HMAC signature. A duplicate integration/event ID is not processed twice. Raw payloads are persisted for simulation; protect production deployments with data retention and sensitive-data policies.
- Incident creation deduplicates unresolved recurring incidents by integration/error where possible. Status, severity, assignment, comments, and transitions are recorded; closed/resolved incidents retain history.
- SLA calculations query persisted execution records in the requested UTC time window. Availability and latency targets are configuration, not hardcoded metric values. Empty windows report unavailable/null metrics as appropriate.
- Migration records describe simulated workflows. Status changes append history; no real provider infrastructure is changed.
- Passwords and JWT/webhook signing secrets are never stored in plaintext application tables or written to logs. Password hashing is handled by ASP.NET Core Identity.
