using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TramitesAbacEvaluatorTests
{
    private static readonly Guid Actor = Guid.Parse("01930201-0001-7001-8001-000000000012");
    private static readonly Guid Other = Guid.Parse("01930201-0001-7001-8001-000000000099");

    [Fact]
    public void OwnOnly_DeniesWhenResourceOwnerDiffers()
    {
        var json = """{"resource":"tramite","constraint":"own_only"}""";
        var ctx = new AbacEvaluationContext(Actor, Other, "tramite", DateTimeOffset.UtcNow);

        TramitesAbacEvaluator.EvaluateJson(json, ctx).Should().BeFalse();
    }

    [Fact]
    public void OwnOnly_AllowsSameOwner()
    {
        var json = """{"resource":"tramite","constraint":"own_only"}""";
        var ctx = new AbacEvaluationContext(Actor, Actor, "tramite", DateTimeOffset.UtcNow);

        TramitesAbacEvaluator.EvaluateJson(json, ctx).Should().BeTrue();
    }

    [Fact]
    public void ResourceType_DeniesWhenMismatch()
    {
        var json = """{"resource":"tramite"}""";
        var ctx = new AbacEvaluationContext(Actor, Actor, "ot", DateTimeOffset.UtcNow);

        TramitesAbacEvaluator.EvaluateJson(json, ctx).Should().BeFalse();
    }

    [Fact]
    public void IsGranted_AllowsWhenAnyRoleHasNoConditions()
    {
        var list = new List<string?> { """{"constraint":"own_only"}""", null };
        var ctx = new AbacEvaluationContext(Actor, Other, "tramite", DateTimeOffset.UtcNow);

        TramitesAbacEvaluator.IsGranted(list, ctx).Should().BeTrue();
    }
}
