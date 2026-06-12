using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class EvaluateOtRules9699Tests
{
    [Fact]
    public void RuleConditionEvaluator_BlocksWhenSoatVigenteFalse()
    {
        var tree = JsonDocument.Parse(
            """{"op":"AND","children":[{"field":"soat_vigente","operator":"equal","value":{"kind":"static","value":"false"}}]}""");

        var fields = new Dictionary<string, string?> { ["soat_vigente"] = "false" };

        RuleConditionEvaluator.Evaluate(tree.RootElement, fields).Should().BeTrue();
    }

    [Fact]
    public void RuleConditionEvaluator_DoesNotMatchWhenSoatVigenteTrue()
    {
        var tree = JsonDocument.Parse(
            """{"op":"AND","children":[{"field":"soat_vigente","operator":"equal","value":{"kind":"static","value":"false"}}]}""");

        var fields = new Dictionary<string, string?> { ["soat_vigente"] = "true" };

        RuleConditionEvaluator.Evaluate(tree.RootElement, fields).Should().BeFalse();
    }
}
