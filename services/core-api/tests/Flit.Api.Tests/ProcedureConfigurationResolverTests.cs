using Flit.Modules.ProceduresConfig.Adapters;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class ProcedureConfigurationResolverTests
{
    [Fact]
    public void Resolve_ReturnsNull_WhenGlobalInactive()
    {
        var bundle = BuildMinimalBundle(globalIsActive: false);

        var result = ProcedureConfigurationResolver.Resolve(bundle);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenTenantActivationMissing()
    {
        var bundle = BuildMinimalBundle() with { TenantActivation = null };

        var result = ProcedureConfigurationResolver.Resolve(bundle);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_IncludesGlobalLayer_WhenTenantActiveWithoutOverrides()
    {
        var bundle = BuildMinimalBundle(
            tenantOverrides: "{}",
            otActivation: null);

        var result = ProcedureConfigurationResolver.Resolve(bundle);

        result.Should().NotBeNull();
        result!.ResolutionLayers.Should().Equal("global");
        result.Scope.Should().Be("global");
    }

    [Fact]
    public void Resolve_CompanyOverride_AppliesTenantOverridesJson()
    {
        var bundle = BuildMinimalBundle(
            tenantOverrides: """{"edges":{"comprador":{"roleLabel":"Comprador (compañía)"}}}""",
            otActivation: null);

        var result = ProcedureConfigurationResolver.Resolve(bundle);

        result.Should().NotBeNull();
        result!.Scope.Should().Be("company");
        result.ResolutionLayers.Should().Equal("global", "company");
        result.Edges.Should().ContainSingle(e =>
            e.Code == "comprador" && e.RoleLabel == "Comprador (compañía)");
    }

    [Fact]
    public async Task Resolve_OtOverride_PrevailsOverCompanyLayer()
    {
        var repo = new InMemoryProceduresConfigReadRepository();
        var bundle = await repo.LoadConfigurationBundleAsync(
            InMemoryProceduresConfigReadRepository.DemoTenantId,
            "TRA_ESTANDAR",
            InMemoryProceduresConfigReadRepository.DemoOtBogotaId,
            TestContext.Current.CancellationToken);

        bundle.Should().NotBeNull();

        var result = ProcedureConfigurationResolver.Resolve(bundle!);

        result.Should().NotBeNull();
        result!.Scope.Should().Be("ot");
        result.ResolutionLayers.Should().Equal("global", "company", "ot");

        var simitComprador = result.Queries.Single(q =>
            q.ConnectorCode == "SIMIT" &&
            q.EdgeCode == "comprador" &&
            q.PersonKindFilter == "natural");
        simitComprador.IsMandatory.Should().BeFalse("OT override debe prevalecer sobre config global");
    }

    [Fact]
    public void ValidateSuperAdmin_ReturnsForbiddenCode_WhenNotSuperAdmin()
    {
        UpdateAdminProcedureTypeGlobal.ValidateSuperAdmin(isSuperAdmin: false)
            .Should()
            .Be(UpdateAdminProcedureTypeGlobal.ForbiddenNotSuperAdminCode);
    }

    [Fact]
    public void ValidateSuperAdmin_ReturnsNull_WhenSuperAdmin()
    {
        UpdateAdminProcedureTypeGlobal.ValidateSuperAdmin(isSuperAdmin: true)
            .Should()
            .BeNull();
    }

    private static ProcedureConfigurationBundle BuildMinimalBundle(
        bool globalIsActive = true,
        string tenantOverrides = "{}",
        ProcedureActivationLayer? otActivation = null) =>
        new(
            Guid.Parse("00000000-0000-7000-8002-000000000010"),
            "TEST_TYPE",
            "test-type",
            "Tipo prueba",
            "TRASPASO",
            2,
            globalIsActive,
            new ProcedureActivationLayer(null, tenantOverrides),
            otActivation,
            [
                new("vehiculo", "Vehículo", "vehicle", true, true, 1, "Vehículo"),
                new("comprador", "Comprador", "person", true, true, 2, "Comprador"),
            ],
            [],
            [
                new("SIMIT", "comprador", true, false, "natural", 1),
            ],
            []);
}
