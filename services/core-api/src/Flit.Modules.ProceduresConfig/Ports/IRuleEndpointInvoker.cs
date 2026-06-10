using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Ports;

/// <summary>Resultado de invocar un endpoint desde una regla (AC2 #9441 runtime).</summary>
public sealed record RuleEndpointInvocationResult(
    string EndpointCode,
    int? HttpStatus,
    bool Succeeded,
    bool RateLimited,
    string? ResponsePreview);

/// <summary>Invoca endpoints del catálogo cuando una regla dispara call_endpoint (AC2 #9439).</summary>
public interface IRuleEndpointInvoker
{
    Task<RuleEndpointInvocationResult?> InvokeFromRuleAsync(
        Guid tenantId,
        string endpointCode,
        Guid? procedureInstanceId,
        Guid ruleId,
        string ruleName,
        CancellationToken ct = default);
}

public sealed class NoOpRuleEndpointInvoker : IRuleEndpointInvoker
{
    public Task<RuleEndpointInvocationResult?> InvokeFromRuleAsync(
        Guid tenantId,
        string endpointCode,
        Guid? procedureInstanceId,
        Guid ruleId,
        string ruleName,
        CancellationToken ct = default) =>
        Task.FromResult<RuleEndpointInvocationResult?>(null);
}
