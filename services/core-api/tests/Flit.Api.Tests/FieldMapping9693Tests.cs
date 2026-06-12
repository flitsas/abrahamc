using System.Text.Json;
using Flit.Modules.ProceduresConfig.Adapters;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class FieldMappingValidatorTests
{
    [Fact]
    public void TryValidate_AcceptsEmptyObject()
    {
        var mapping = JsonRuleElements.Parse("{}");
        FieldMappingValidator.TryValidate(mapping, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_AcceptsValidMapping()
    {
        var mapping = JsonRuleElements.Parse("""{"placa":"vehicle.plate","doc_propietario":"owner.document"}""");
        FieldMappingValidator.TryValidate(mapping, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"InvalidKey":"path.value"}""")]
    [InlineData("""{"placa":123}""")]
    [InlineData("""{"placa":""}""")]
    [InlineData("""{"placa":"bad-segment"}""")]
    public void TryValidate_RejectsInvalidMapping(string json)
    {
        var mapping = JsonRuleElements.Parse(json);
        FieldMappingValidator.TryValidate(mapping, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }
}

public sealed class FieldMappingTransformerTests
{
    [Fact]
    public void Transform_MapsResponsePathsToFormKeys()
    {
        var mapping = JsonRuleElements.Parse("""{"placa":"vehicle.plate","vin":"vehicle.vin"}""");
        var response = JsonRuleElements.Parse("""{"vehicle":{"plate":"ABC123","vin":"VIN001"}}""");

        var result = FieldMappingTransformer.Transform(mapping, response);

        result.Should().ContainKey("placa").WhoseValue.Should().Be("ABC123");
        result.Should().ContainKey("vin").WhoseValue.Should().Be("VIN001");
    }

    [Fact]
    public void Transform_ReturnsEmpty_WhenMappingEmpty()
    {
        var mapping = JsonRuleElements.Parse("{}");
        var response = JsonRuleElements.Parse("""{"vehicle":{"plate":"ABC123"}}""");

        FieldMappingTransformer.Transform(mapping, response).Should().BeEmpty();
    }
}

public sealed class EndpointCatalogFieldMappingUseCaseTests
{
    [Fact]
    public async Task CreateEndpointCatalogEntry_Returns400Validation_WhenFieldMappingInvalid()
    {
        var repo = new InMemoryEndpointCatalogRepository();
        var tenantId = InMemoryProcedureRulesRepository.DemoTenantId;
        var invalidMapping = JsonRuleElements.Parse("""{"Bad-Key":"x.y"}""");

        var result = await CreateEndpointCatalogEntry.HandleAsync(
            new CreateEndpointCatalogEntry.Command(
                tenantId,
                "RUNT_LOOKUP",
                "RUNT Lookup",
                "https://example.test/runt",
                "GET",
                "none",
                JsonRuleElements.Parse("{}"),
                invalidMapping,
                5000,
                true,
                Guid.NewGuid()),
            repo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Kind.Should().Be(EndpointCatalogErrorKind.Validation);
    }
}
