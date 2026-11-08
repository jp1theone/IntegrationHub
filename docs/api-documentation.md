# API documentation

Base URL for the local HTTP profile: `http://localhost:5121`. Swagger UI: `http://localhost:5121/swagger` (Development only). All administrative APIs require a bearer JWT except login, health checks, and signed webhook receipt. List routes use `page`/`pageSize` where applicable and return `{ items, page, pageSize, totalItems, totalPages }`.

## Route groups

| Method | Route | Access / purpose |
|---|---|---|
| `POST` | `/api/auth/login` | Public; issue JWT |
| `GET` | `/api/auth/me` | Authenticated; current user/roles |
| `POST` | `/api/auth/logout` | Authenticated; client clears local token |
| `GET, POST` | `/api/clients` | View roles / integration managers |
| `GET, PUT, PATCH` | `/api/clients/{id}` and `/status` | Client detail, update and activate/deactivate |
| `GET, POST` | `/api/providers` | View roles / integration managers |
| `GET, PUT, PATCH` | `/api/providers/{id}`, `/status`, `/health` | Provider detail, update, activate and health simulation |
| `GET, POST` | `/api/integrations` | View roles / integration managers |
| `GET, PUT, PATCH` | `/api/integrations/{id}`, `/status` | Detail, update, activate/status |
| `GET` | `/api/integrations/{id}/executions`, `/sla` | Related history and SLA |
| `POST` | `/api/integrations/{id}/execute` | Integration managers; execute a JSON object; optional `Idempotency-Key` header |
| `GET` | `/api/executions`, `/api/executions/{id}` | Search and inspect execution/attempt history |
| `POST` | `/api/executions/{id}/retry` | Administrator, integration or support analyst |
| `GET` | `/api/executions/{id}/logs`, `/api/logs` | Operational log search |
| `POST` | `/api/webhooks/{integrationCode}` | Public callback with HMAC `X-Webhook-Signature` and dedupe `X-Event-ID` |
| `GET` | `/api/webhook-events`, `/api/webhook-events/{id}` | Authenticated event monitoring |
| `GET, POST` | `/api/incidents` | View / manager creates |
| `GET` | `/api/incidents/{id}` | Incident and comments |
| `PATCH` | `/api/incidents/{id}/status`, `/assignment`, `/severity` | Role-authorized incident workflow |
| `POST` | `/api/incidents/{id}/comments` | Administrator, integration or support analyst |
| `GET, POST` | `/api/migrations` | View / integration manager creates |
| `GET, PUT, PATCH` | `/api/migrations/{id}` and `/status` | Detail, update and status history |
| `GET` | `/api/dashboard/summary`, `/executions`, `/incidents`, `/integrations`, `/providers` | Live persisted dashboard/report metrics |
| `GET` | `/api/reports/executions`, `/incidents`, `/sla` | Operational reports |
| `GET, POST` | `/api/users` | Administrator only |
| `GET, PUT, PATCH` | `/api/users/{id}` and `/status` | Administrator user management |
| `GET` | `/api/system/runtime` | Authenticated; active runtime mode and registered adapter types (no secrets) |
| `GET` | `/health`, `/api/health` | Anonymous liveness/readiness endpoint |

## Login example

```http
POST /api/auth/login
Content-Type: application/json

{"email":"admin@integrationhub.local","password":"<your-local-seed-password>"}
```

The response contains `accessToken`, `expiresAt`, and a `user` DTO. Send later calls with `Authorization: Bearer <accessToken>`.

## Execution example

```http
POST /api/integrations/{integrationId}/execute
Authorization: Bearer <accessToken>
Idempotency-Key: order-demo-1001
Content-Type: application/json

{"payload":{"reference":"order-demo-1001","amount":42.50}}
```

Response status is `200` for success, `202` when retry is scheduled, or `502` for a terminal provider failure. The response includes execution ID, correlation ID, status, response/error detail, attempt number and next retry time. A successful replay of the same idempotency key returns the persisted successful operation.

## Webhook signature example

The HMAC SHA-256 signature is `sha256=` followed by lowercase hex of HMAC(secret, exact UTF-8 request body). Supply `X-Event-ID` for deduplication. Example PowerShell:

```powershell
$body = '{"event":"order.updated","reference":"demo-1"}'
$secret = '<your-local-webhook-secret>'
$hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$signature = 'sha256=' + [Convert]::ToHexString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).ToLowerInvariant()
Invoke-RestMethod -Method Post -Uri 'http://localhost:5121/api/webhooks/INT-001' -ContentType 'application/json' -Headers @{ 'X-Webhook-Signature'=$signature; 'X-Event-ID'='demo-event-1' } -Body $body
```

Errors use `ProblemDetails` with suitable HTTP status. Webhook bodies are limited to 256 KiB. Full schemas and authorization requirements are available in Swagger.
