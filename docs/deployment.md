# Deployment preparation

No cloud resource has been created and no deployment has been run. `.github/workflows/deploy-api.yml` is a manual Azure App Service template that requires repository configuration and secrets.

For local development, configure a SQL Server 2019-or-newer instance and a dedicated `IntegrationHub` database. EF Core's SQL Server provider lists SQL Server 2019 onwards as supported. Choose an edition and license appropriate for your use. Production must use an appropriately licensed managed database or SQL Server deployment with separate credentials, networking and backups. See the [provider compatibility reference](https://learn.microsoft.com/ef/core/providers/sql-server/).

## Required production resources

- Azure App Service (Linux, .NET 10 runtime) or another .NET 10 host; configure HTTPS and a custom domain if needed.
- Azure SQL Database or managed SQL Server with private networking/firewall controls, least privilege, automated backups, restore testing, and schema migration plan.
- Managed secret store such as Azure Key Vault for connection string, 256-bit JWT signing key, and webhook HMAC key. Do not use demo seed credentials in production.
- Static web hosting for the React build behind HTTPS. Set `VITE_API_BASE_URL` at build time to the public API base URL and configure matching API CORS origins.
- Centralized logs/metrics, health probes, alerts, retention, and incident ownership.

## Configuration checklist

1. Provision a separate production SQL database and restricted application principal.
2. Store `ConnectionStrings__IntegrationHub`, `Jwt__SigningKey`, and `Webhooks__SigningSecret` in the managed secret store. Generate independent random secrets and define rotation procedures.
3. Set `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes`, `Cors__AllowedOrigins`, retry, rate-limit, and SLA configuration explicitly.
4. Set `ASPNETCORE_ENVIRONMENT=Production`; keep migrations disabled on API startup. Apply reviewed EF migration scripts through a controlled release step.
5. Keep `Seed:DemoPassword` unset. Provision production users through a reviewed administrative process.
6. Build/publish the API and web application separately. Configure TLS, secure headers, backups, alerts, and log scrubbing.
7. Run smoke tests in staging and verify migration rollback/restore before production release.
8. Set `IntegrationRuntime__Mode=Production` only after the required real provider adapters are registered and staged. Add only exact HTTPS hostnames to `IntegrationRuntime__AllowedHosts__0` (additional hosts use incrementing indexes), and configure each provider's public HTTPS `BaseUrl`. The checked-in `appsettings.Production.json` selects Production and intentionally has no provider URLs or credentials; an empty allowlist blocks all external connections.

## Provider adapter readiness

The application has two runtime modes controlled by the API, not by the browser:

- `Simulation` (default): routes all provider executions to `DeterministicMockIntegrationProvider`. Outcomes remain fictional and reproducible.
- `Production`: routes only to an `IProductionIntegrationProvider` registered for the provider's `ProviderType`. A provider must also have an HTTPS `BaseUrl` on the API's exact hostname allowlist. There is no automatic simulator fallback. Missing endpoints, allowlist entries or adapters fail closed without making a network request.

No real FUDO, TOTEAT, or generic REST connector is included yet. To add one, implement and register an adapter in Infrastructure, use typed `HttpClient` with TLS validation and a strict endpoint allowlist, load credentials from Key Vault or the deployment secret manager, apply vendor-specific authentication/signature and idempotency rules, redact logs, and add adapter contract, timeout, retry and health tests. Do not store API keys or tokens in the `Providers` table or in the React application. Validate first against the vendor sandbox and a separate staging environment.

## Azure workflow setup

The manual workflow is restricted to `main` and requires typing `deploy`. It uses GitHub Actions secret `AZURE_WEBAPP_PUBLISH_PROFILE` and variable `AZURE_WEBAPP_NAME`. Configure those only after creating the App Service, review the workflow's branch and artifact settings, and add runtime secrets through App Service configuration/Key Vault. Do not enable automatic production release until approvals, environments, and branch protection are configured.

## Frontend deployment

Run `npm ci && npm run build` in `src/IntegrationHub.Web`; publish `dist/` to a static host. Set `VITE_API_BASE_URL` at build time, for example `https://api.example.com/api`, and set API `Cors:AllowedOrigins` to the exact frontend origin. Vite environment values are public bundle configuration; never put secrets there.

## Limitations

The current app is a portfolio simulator. It does not include a production message broker, distributed worker lock/queue, refresh-token revocation, full observability backend, formal threat model, or cloud-infrastructure-as-code. Complete those reviews and operational controls before using this code for real business data.
