using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Infrastructure.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/system/runtime")]
[Authorize]
public sealed class RuntimeController(
    IOptions<IntegrationRuntimeOptions> options,
    IEnumerable<IProductionIntegrationProvider> adapters) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var registeredTypes = adapters.Select(adapter => adapter.ProviderType.ToString()).Order().ToArray();
        var isProduction = options.Value.Mode == IntegrationRuntimeMode.Production;
        return Ok(new RuntimeModeResponse(
            options.Value.Mode.ToString(),
            isProduction,
            registeredTypes,
            isProduction && registeredTypes.Length == 0,
            isProduction
                ? "Production mode never falls back to the simulator. Providers without an adapter are blocked."
                : "All provider requests use deterministic simulations; no external systems are called."));
    }
}

public sealed record RuntimeModeResponse(string Mode, bool IsProduction, IReadOnlyCollection<string> RegisteredProductionAdapters,
    bool ProductionAdaptersMissing, string SafetyMessage);
