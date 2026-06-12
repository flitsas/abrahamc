using Flit.Modules.ProceduresConfig.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class ProcedurePipelineValidatorTests
{
    [Fact]
    public void ValidateMinActiveDataEdges_ReturnsValid_WhenFourOrMoreActiveNonDocumentEdges()
    {
        var edges = new (string EdgeCode, bool IsActive)[]
        {
            ("vehiculo", true),
            ("propietario", true),
            ("comprador", true),
            ("locatario", true),
            ("documentos", true),
        };

        var (isValid, error) = ProcedurePipelineValidator.ValidateMinActiveDataEdges(edges);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateMinActiveDataEdges_ReturnsInvalid_WhenLessThanFourActiveNonDocumentEdges()
    {
        var edges = new (string EdgeCode, bool IsActive)[]
        {
            ("vehiculo", true),
            ("propietario", true),
            ("comprador", true),
            ("documentos", true),
        };

        var (isValid, error) = ProcedurePipelineValidator.ValidateMinActiveDataEdges(edges);

        isValid.Should().BeFalse();
        error.Should().Contain("al menos 4 pasos activos");
        error.Should().Contain("Actual: 3");
    }

    [Fact]
    public void ValidateMinActiveDataEdges_ExcludesDocumentos_EvenWhenActive()
    {
        var edges = new (string EdgeCode, bool IsActive)[]
        {
            ("vehiculo", true),
            ("propietario", true),
            ("comprador", true),
            ("locatario", false),
            ("documentos", true),
        };

        var (isValid, error) = ProcedurePipelineValidator.ValidateMinActiveDataEdges(edges);

        isValid.Should().BeFalse();
        error.Should().Contain("Actual: 3");
    }

    [Fact]
    public void ValidateMinActiveDataEdges_IsCaseInsensitive_ForDocumentosEdge()
    {
        var edges = new (string EdgeCode, bool IsActive)[]
        {
            ("vehiculo", true),
            ("propietario", true),
            ("comprador", true),
            ("DOCUMENTOS", true),
        };

        var (isValid, error) = ProcedurePipelineValidator.ValidateMinActiveDataEdges(edges);

        isValid.Should().BeFalse();
        error.Should().Contain("Actual: 3");
    }
}
