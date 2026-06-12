using Flit.Infrastructure.Adapters;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class QuipuxWebhook9698Tests
{
    [Fact]
    public void ValidateSignature_AcceptsMockSignature()
    {
        var adapter = new QuipuxMockWebhookAdapter();
        adapter.ValidateSignature(QuipuxMockWebhookAdapter.MockSignature, "{}").Should().BeTrue();
    }

    [Fact]
    public void ValidateSignature_RejectsInvalidSignature()
    {
        var adapter = new QuipuxMockWebhookAdapter();
        adapter.ValidateSignature("wrong", "{}").Should().BeFalse();
    }

    [Fact]
    public void ParseAndMap_ExtractsProcedureIdAndState()
    {
        var adapter = new QuipuxMockWebhookAdapter();
        var procedureId = Guid.Parse("01930101-0001-7001-8001-000000000001");
        var result = adapter.ParseAndMap(
            "tramite_estado_cambiado",
            $$"""{"tramite_id":"{{procedureId}}","state":"aprobado"}""");

        result.ProcedureUpdated.Should().BeTrue();
        result.ProcedureId.Should().Be(procedureId);
        result.NewState.Should().Be("aprobado");
    }
}
