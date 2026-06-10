using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

public sealed class CatalogRuleEndpointInvoker(
    IEndpointCatalogRepository catalogRepo,
    IEndpointCallLogRepository callLogRepo,
    EndpointInvocationRateLimiter rateLimiter,
    IHttpClientFactory httpClientFactory) : IRuleEndpointInvoker
{
    public async Task<RuleEndpointInvocationResult?> InvokeFromRuleAsync(
        Guid tenantId,
        string endpointCode,
        Guid? procedureInstanceId,
        Guid ruleId,
        string ruleName,
        CancellationToken ct = default)
    {
        var payload = JsonRuleElements.Parse(
            JsonSerializer.Serialize(new { rule_id = ruleId, rule_name = ruleName }));

        var result = await InvokeCatalogEndpoint.HandleAsync(
            new InvokeCatalogEndpoint.Command(tenantId, endpointCode, payload, procedureInstanceId),
            catalogRepo,
            callLogRepo,
            rateLimiter,
            httpClientFactory,
            ct);

        return result.Match(
            ok => new RuleEndpointInvocationResult(
                ok.EndpointCode,
                ok.HttpStatus,
                ok.Succeeded,
                ok.RateLimited,
                ok.ResponsePreview),
            _ => new RuleEndpointInvocationResult(
                endpointCode,
                null,
                false,
                RateLimited: false,
                ResponsePreview: null));
    }
}
