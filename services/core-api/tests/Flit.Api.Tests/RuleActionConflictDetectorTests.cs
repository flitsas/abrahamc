using System.Text.Json;
using Flit.Modules.ProceduresConfig.Adapters;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class RuleActionConflictDetectorTests
{
    [Fact]
    public void Detect_ReturnsConflict_WhenBlockAndInjectSectionShareTarget()
    {
        var blockAction = Action("block", """{"target":"comprador_datos","message":"Bloqueado"}""");
        var injectAction = Action("inject_section", """{"target":"comprador_datos","mode":"hide"}""");

        var conflicts = RuleActionConflictDetector.Detect([
            (Guid.Parse("00000000-0000-7000-8001-000000000001"), blockAction),
            (Guid.Parse("00000000-0000-7000-8001-000000000002"), injectAction),
        ]);

        conflicts.Should().ContainSingle(c =>
            c.ConflictType == RuleActionConflictDetector.BlockVsInjectSection &&
            c.Target == "comprador_datos");
    }

    [Fact]
    public void Detect_ReturnsGlobalConflict_WhenGlobalBlockAndInjectSectionPresent()
    {
        var blockAction = Action("block", """{"message":"Bloqueo global"}""");
        var injectAction = Action("inject_section", """{"section_code":"DESCUENTOS","mode":"show"}""");

        var conflicts = RuleActionConflictDetector.Detect([
            (Guid.Parse("00000000-0000-7000-8001-000000000001"), blockAction),
            (Guid.Parse("00000000-0000-7000-8001-000000000002"), injectAction),
        ]);

        conflicts.Should().ContainSingle(c =>
            c.ConflictType == RuleActionConflictDetector.GlobalBlockVsInjectSection &&
            c.Target == "__global__");
    }

    [Fact]
    public void Detect_ReturnsEmpty_WhenNoConflictingActions()
    {
        var injectA = Action("inject_section", """{"target":"seccion_a","mode":"hide"}""");
        var injectB = Action("inject_section", """{"target":"seccion_b","mode":"show"}""");

        var conflicts = RuleActionConflictDetector.Detect([
            (Guid.Parse("00000000-0000-7000-8001-000000000001"), injectA),
            (Guid.Parse("00000000-0000-7000-8001-000000000002"), injectB),
        ]);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task SimulateProcedureRules_ReturnsConflicts_WhenBlockAndInjectShareTarget()
    {
        var catalog = new InMemoryProcedureRulesCatalogRepository();
        var rulesRepo = new InMemoryProcedureRulesRepository(catalog);

        await catalog.AddAsync(
            new ProcedureRuleWriteModel(
                null,
                InMemoryProcedureRulesRepository.DemoTenantId,
                InMemoryProcedureRulesRepository.DemoProcedureTypeId,
                "Bloqueo comprador demo",
                null,
                InMemoryProcedureRulesRepository.DemoConditionTree.GetRawText(),
                """[{"type":"block","target":"DESCUENTOS_VERDES","message":"No permitido"}]""",
                5,
                true,
                Guid.Empty),
            TestContext.Current.CancellationToken);

        var response = await SimulateProcedureRules.HandleAsync(
            new SimulateProcedureRules.Query(
                InMemoryProcedureRulesRepository.DemoTenantId,
                InMemoryProcedureRulesRepository.DemoProcedureTypeId,
                new Dictionary<string, string?> { ["marca"] = "Tesla" }),
            rulesRepo,
            TestContext.Current.CancellationToken);

        response.MatchedRules.Should().HaveCountGreaterThan(1);
        response.Conflicts.Should().Contain(c =>
            c.ConflictType == RuleActionConflictDetector.BlockVsInjectSection &&
            c.Target == "DESCUENTOS_VERDES");
    }

    [Fact]
    public async Task SimulateProcedureRules_DoesNotPersistExecutionLogs()
    {
        var catalog = new InMemoryProcedureRulesCatalogRepository();
        var rulesRepo = new InMemoryProcedureRulesRepository(catalog);
        var logRepo = new InMemoryRuleExecutionLogRepository();

        _ = await SimulateProcedureRules.HandleAsync(
            new SimulateProcedureRules.Query(
                InMemoryProcedureRulesRepository.DemoTenantId,
                InMemoryProcedureRulesRepository.DemoProcedureTypeId,
                new Dictionary<string, string?> { ["marca"] = "Tesla" }),
            rulesRepo,
            TestContext.Current.CancellationToken);

        logRepo.Entries.Should().BeEmpty();
    }

    private static EvaluateProcedureRules.RuleActionResult Action(string type, string paramsJson)
    {
        using var doc = JsonDocument.Parse(paramsJson);
        return new EvaluateProcedureRules.RuleActionResult(type, doc.RootElement.Clone());
    }
}
