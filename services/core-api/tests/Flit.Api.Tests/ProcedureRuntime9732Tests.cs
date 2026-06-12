using Flit.Modules.Procedures.Application;
using Flit.Modules.Procedures.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class ProcedureRuntime9732Tests
{
    [Theory]
    [InlineData("TRASP", "02", "EVE", 8841, "TRASP-02_EVE-8841")]
    [InlineData("TRAES", "AN", "BOG", 1, "TRAES-AN_BOG-1")]
    public void GenerateReferenceNumber_ProducesCompositeFormat(
        string typeCode,
        string tenantCode,
        string otCode,
        int sequence,
        string expected)
    {
        var result = FileProcedureInstance.GenerateReferenceNumber(typeCode, tenantCode, otCode, sequence);
        result.Should().Be(expected);
    }

    [Fact]
    public void AbbreviateProcedureTypeCode_KnownTypes_ReturnExpected()
    {
        ProcedureReferenceFormatter.AbbreviateProcedureTypeCode("TRA_ESTANDAR").Should().Be("TRASP");
        ProcedureReferenceFormatter.AbbreviateProcedureTypeCode("MAT_LEASING").Should().Be("MATLE");
    }

    [Fact]
    public async Task ValidateProcedureOwnership_Sum100_IsValid()
    {
        var instanceRepo = new Flit.Modules.Procedures.Adapters.InMemoryProcedureInstanceRepository();
        var actorRepo = new Flit.Modules.Procedures.Adapters.InMemoryProcedureActorRepository();
        var tenantId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await instanceRepo.SaveAsync(CreateInstance(instanceId, tenantId), CancellationToken.None);
        await actorRepo.ReplaceOwnersAsync(
            tenantId,
            instanceId,
            SaveProcedureOwners.PropietarioEdgeRole,
            [
                CreateOwner(tenantId, instanceId, userId, 0, 60m),
                CreateOwner(tenantId, instanceId, userId, 1, 40m),
            ],
            CancellationToken.None);

        var result = await ValidateProcedureOwnership.HandleAsync(
            new ValidateProcedureOwnership.Query(instanceId, tenantId),
            instanceRepo,
            actorRepo,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.TotalPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task ValidateProcedureOwnership_SumNot100_IsInvalid()
    {
        var instanceRepo = new Flit.Modules.Procedures.Adapters.InMemoryProcedureInstanceRepository();
        var actorRepo = new Flit.Modules.Procedures.Adapters.InMemoryProcedureActorRepository();
        var tenantId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await instanceRepo.SaveAsync(CreateInstance(instanceId, tenantId), CancellationToken.None);
        await actorRepo.ReplaceOwnersAsync(
            tenantId,
            instanceId,
            SaveProcedureOwners.PropietarioEdgeRole,
            [
                CreateOwner(tenantId, instanceId, userId, 0, 50m),
                CreateOwner(tenantId, instanceId, userId, 1, 30m),
            ],
            CancellationToken.None);

        var result = await ValidateProcedureOwnership.HandleAsync(
            new ValidateProcedureOwnership.Query(instanceId, tenantId),
            instanceRepo,
            actorRepo,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.TotalPercentage.Should().Be(80m);
        result.Value.Errors.Should().Contain(e => e.Contains("100"));
    }

    [Fact]
    public void ReadSnapshotMetadata_ReadsMandatoryAndCircuitOpen()
    {
        var json = System.Text.Json.JsonDocument.Parse(
            """{"mandatory":true,"circuitOpen":false,"data":{}}""").RootElement;

        var (mandatory, circuitOpen) = GetProcedureQueryResults.ReadSnapshotMetadata(json);
        mandatory.Should().BeTrue();
        circuitOpen.Should().BeFalse();
    }

    private static ProcedureInstance CreateInstance(Guid id, Guid tenantId) => new(
        id,
        tenantId,
        Guid.NewGuid(),
        null,
        "TRASP-01_GEN-1",
        ProcedureStates.Borrador,
        """{"version":1}""",
        1,
        null,
        DateTimeOffset.UtcNow,
        0m,
        "COP",
        DateTimeOffset.UtcNow,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        Guid.NewGuid(),
        1);

    private static Flit.Modules.Procedures.Ports.ProcedureActorEntry CreateOwner(
        Guid tenantId,
        Guid instanceId,
        Guid userId,
        short sequence,
        decimal percentage) => new(
        Guid.NewGuid(),
        tenantId,
        instanceId,
        SaveProcedureOwners.PropietarioEdgeRole,
        "natural",
        "CC",
        $"DOC-{sequence}",
        $"Owner {sequence}",
        percentage,
        sequence,
        userId,
        userId);
}
