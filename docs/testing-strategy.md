# Testing strategy

## Commands

```powershell
dotnet restore .\IntegrationHub.sln
dotnet build .\IntegrationHub.sln --no-restore
dotnet test .\IntegrationHub.sln --no-build --no-restore
Set-Location .\src\IntegrationHub.Web
npm ci
npm run build
```

## Layers

- Unit tests assert domain normalization/state rules and all deterministic mock provider outcomes. These run quickly and do not access a database.
- Integration tests host the actual API using `WebApplicationFactory<Program>`, override SQL Server settings with an isolated, per-factory in-memory SQLite database, and seed test-only users. Database initialization is serialized because the API factory changes process-level test configuration. They verify health, login, authorized create, forbidden access, invalid credentials, successful execution/idempotency replay, signed webhook intake/deduplication, and rejection of invalid webhook signatures.
- No test uses the local SQL Server, Docker database, or NexusPay resources. A keeper SQLite connection lives only for each test factory lifetime.
- Frontend verification currently includes Oxlint and the strict TypeScript/Vite production build. There is no frontend unit or browser end-to-end test script yet.

## Current coverage and gaps

Current verified suite: 15 unit tests and 8 API integration tests (`dotnet test IntegrationHub.sln -c Release`). Frontend lint and production build pass; `npm audit --omit=dev` reports no production dependency vulnerabilities. Coverage remains incomplete for concurrent retries, incident lifecycle, migration transitions, SLA boundary conditions, and broader authorization cases. Add those cases and browser-level acceptance tests before production use. This suite alone is not a production safety assessment.
