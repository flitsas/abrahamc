using System.Text.Json;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

public static class ListVehicleOwnershipRules
{
    public sealed record Query(Guid TenantId);

    public sealed record RuleDto(
        Guid Id,
        string Name,
        string RuleType,
        string ConditionJson,
        int Priority,
        bool IsActive);

    public static async Task<IReadOnlyList<RuleDto>> HandleAsync(
        Query query,
        IVehicleOwnershipRulesRepository repo,
        CancellationToken ct = default)
    {
        var rules = await repo.ListActiveByTenantAsync(query.TenantId, ct);
        return rules.Select(r => new RuleDto(
            r.Id, r.Name, r.RuleType, r.ConditionJson, r.Priority, r.IsActive)).ToList();
    }
}

public enum UpsertVehicleRuleErrorCode
{
    InvalidRuleType,
    InvalidConditionJson,
    NotFound,
    WrongTenant,
}

public sealed record UpsertVehicleRuleError(UpsertVehicleRuleErrorCode Code, string Message);

public static class CreateVehicleOwnershipRule
{
    public sealed record Command(
        Guid TenantId,
        string Name,
        string RuleType,
        string ConditionJson,
        int Priority,
        bool IsActive,
        Guid ActorUserId);

    public static async Task<Result<Guid, UpsertVehicleRuleError>> HandleAsync(
        Command cmd,
        IVehicleOwnershipRulesRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        if (!VehicleOwnershipRuleType.All.Contains(cmd.RuleType))
        {
            return Result<Guid, UpsertVehicleRuleError>.Failure(
                new UpsertVehicleRuleError(UpsertVehicleRuleErrorCode.InvalidRuleType, $"rule_type inválido: {cmd.RuleType}"));
        }

        if (!IsValidJson(cmd.ConditionJson))
        {
            return Result<Guid, UpsertVehicleRuleError>.Failure(
                new UpsertVehicleRuleError(UpsertVehicleRuleErrorCode.InvalidConditionJson, "condition debe ser JSON válido."));
        }

        var rule = VehicleOwnershipRule.Create(
            cmd.TenantId,
            cmd.Name,
            cmd.RuleType,
            cmd.ConditionJson,
            cmd.Priority,
            cmd.ActorUserId,
            clock.UtcNow,
            cmd.IsActive);

        await repo.AddAsync(rule, ct);
        await saveChanges(ct);
        return Result<Guid, UpsertVehicleRuleError>.Success(rule.Id);
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public static class UpdateVehicleOwnershipRule
{
    public sealed record Command(
        Guid RuleId,
        Guid TenantId,
        string Name,
        string RuleType,
        string ConditionJson,
        int Priority,
        bool IsActive,
        Guid ActorUserId);

    public static async Task<Result<Guid, UpsertVehicleRuleError>> HandleAsync(
        Command cmd,
        IVehicleOwnershipRulesRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var rule = await repo.GetByIdAsync(cmd.RuleId, ct);
        if (rule is null)
        {
            return Result<Guid, UpsertVehicleRuleError>.Failure(
                new UpsertVehicleRuleError(UpsertVehicleRuleErrorCode.NotFound, "Regla no encontrada."));
        }

        if (rule.TenantId != cmd.TenantId)
        {
            return Result<Guid, UpsertVehicleRuleError>.Failure(
                new UpsertVehicleRuleError(UpsertVehicleRuleErrorCode.WrongTenant, "La regla no pertenece al tenant."));
        }

        if (!VehicleOwnershipRuleType.All.Contains(cmd.RuleType))
        {
            return Result<Guid, UpsertVehicleRuleError>.Failure(
                new UpsertVehicleRuleError(UpsertVehicleRuleErrorCode.InvalidRuleType, $"rule_type inválido: {cmd.RuleType}"));
        }

        rule.Update(cmd.Name, cmd.RuleType, cmd.ConditionJson, cmd.Priority, cmd.IsActive, cmd.ActorUserId, clock.UtcNow);
        repo.Update(rule);
        await saveChanges(ct);
        return Result<Guid, UpsertVehicleRuleError>.Success(rule.Id);
    }
}

public enum VehicleOwnershipDecision
{
    Allow,
    Block,
}

public sealed record VehicleOwnershipBlockResult(
    string ExceptionCode,
    string Message,
    Guid RuleId,
    string RuleName);

public static class EvaluateVehicleOwnershipInterceptor
{
    public sealed record Command(
        Guid TenantId,
        string Plate,
        string? ProcedureType,
        int? ModelYear,
        string? ApprovedExceptionCode);

    public sealed record WarningDto(Guid RuleId, string RuleName, string Message);

    public sealed record AllowResponse(
        string Decision,
        Guid? MatchedRuleId,
        string? MatchedRuleName,
        IReadOnlyList<WarningDto> Warnings);

    public static async Task<Result<AllowResponse, VehicleOwnershipBlockResult>> HandleAsync(
        Command cmd,
        IVehicleOwnershipRulesRepository repo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Plate))
        {
            return Result<AllowResponse, VehicleOwnershipBlockResult>.Failure(
                new VehicleOwnershipBlockResult(
                    "VOR-MISSING-PLATE",
                    "plate es requerida para evaluar el interceptor.",
                    Guid.Empty,
                    "validation"));
        }

        var rules = await repo.ListActiveByTenantAsync(cmd.TenantId, ct);
        var context = new VehicleOwnershipConditionMatcher.Context(
            cmd.Plate,
            cmd.ProcedureType,
            cmd.ModelYear);

        var warnings = new List<WarningDto>();
        Guid? allowRuleId = null;
        string? allowRuleName = null;

        foreach (var rule in rules)
        {
            if (!VehicleOwnershipConditionMatcher.Matches(rule.ConditionJson, context))
                continue;

            switch (rule.RuleType)
            {
                case VehicleOwnershipRuleType.Block:
                    return Result<AllowResponse, VehicleOwnershipBlockResult>.Failure(
                        new VehicleOwnershipBlockResult(
                            VehicleOwnershipRule.ToExceptionCode(rule.Id),
                            $"Traspaso rechazado por regla «{rule.Name}».",
                            rule.Id,
                            rule.Name));

                case VehicleOwnershipRuleType.RequireException:
                    var expected = VehicleOwnershipRule.ToExceptionCode(rule.Id);
                    if (!string.Equals(cmd.ApprovedExceptionCode, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<AllowResponse, VehicleOwnershipBlockResult>.Failure(
                            new VehicleOwnershipBlockResult(
                                expected,
                                $"Se requiere excepción aprobada ({expected}) para «{rule.Name}».",
                                rule.Id,
                                rule.Name));
                    }

                    break;

                case VehicleOwnershipRuleType.Warn:
                    warnings.Add(new WarningDto(rule.Id, rule.Name, $"Advertencia: {rule.Name}"));
                    break;

                case VehicleOwnershipRuleType.Allow:
                    allowRuleId = rule.Id;
                    allowRuleName = rule.Name;
                    break;
            }
        }

        return Result<AllowResponse, VehicleOwnershipBlockResult>.Success(
            new AllowResponse(
                VehicleOwnershipDecision.Allow.ToString().ToLowerInvariant(),
                allowRuleId,
                allowRuleName,
                warnings));
    }
}
