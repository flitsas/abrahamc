using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TenantGovernanceUseCasesTests
{
    private static readonly Guid TenantId = Guid.Parse("01930110-0001-7001-8001-000000000001");
    private static readonly Guid ValidUserId = Guid.Parse("01930110-0001-7001-8001-000000000002");
    private static readonly Guid InvalidUserId = Guid.Parse("01930120-0001-7001-8001-000000000099");
    private static readonly Guid ExemptUserId = Guid.Parse("01930121-0001-7001-8001-000000000003");
    private static readonly Guid TrafficAgencyId = Guid.Parse("01930100-0001-7001-8001-000000000010");
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    private sealed class InMemoryGovernanceRepo : ITenantGovernanceRepository
    {
        private readonly HashSet<Guid> _tenantUsers = [ValidUserId, ExemptUserId];
        private readonly HashSet<Guid> _exemptUsers = [ExemptUserId];
        private readonly Dictionary<(Guid TenantId, Guid TrafficAgencyId), bool> _otMatrix = [];
        private readonly List<TenantUserExceptionRow> _exceptions = [];

        public void SetOtEnabled(Guid tenantId, Guid trafficAgencyId, bool enabled) =>
            _otMatrix[(tenantId, trafficAgencyId)] = enabled;

        public Task<IReadOnlyList<TenantUserExceptionRow>> ListUserExceptionsAsync(
            Guid tenantId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantUserExceptionRow>>(
                _exceptions.Where(e => e.TenantId == tenantId).ToList());

        public Task<TenantUserExceptionRow?> GetUserExceptionByIdAsync(
            Guid tenantId,
            Guid exceptionId,
            CancellationToken ct = default) =>
            Task.FromResult(_exceptions.FirstOrDefault(e => e.TenantId == tenantId && e.Id == exceptionId));

        public Task<Guid> CreateUserExceptionAsync(
            Guid tenantId,
            Guid userId,
            string? reason,
            DateTimeOffset? expiresAt,
            Guid actorUserId,
            CancellationToken ct = default)
        {
            var id = Guid.CreateVersion7();
            _exceptions.Add(new TenantUserExceptionRow(
                id, tenantId, userId, reason, expiresAt, DateTimeOffset.UtcNow));
            _exemptUsers.Add(userId);
            return Task.FromResult(id);
        }

        public Task<bool> SoftDeleteUserExceptionAsync(
            Guid tenantId,
            Guid exceptionId,
            Guid actorUserId,
            CancellationToken ct = default)
        {
            var row = _exceptions.FirstOrDefault(e => e.TenantId == tenantId && e.Id == exceptionId);
            if (row is null)
            {
                return Task.FromResult(false);
            }

            _exceptions.Remove(row);
            _exemptUsers.Remove(row.UserId);
            return Task.FromResult(true);
        }

        public Task<bool> UserBelongsToTenantAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_tenantUsers.Contains(userId));

        public Task<bool> IsUserExemptFromOnlyOwnAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_exemptUsers.Contains(userId));

        public Task<IReadOnlyList<TenantAuthorizedTrafficAgencyRow>> ListAuthorizedTrafficAgenciesAsync(
            Guid tenantId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantAuthorizedTrafficAgencyRow>>(
                _otMatrix
                    .Where(kv => kv.Key.TenantId == tenantId)
                    .Select(kv => new TenantAuthorizedTrafficAgencyRow(
                        Guid.CreateVersion7(),
                        tenantId,
                        kv.Key.TrafficAgencyId,
                        kv.Value,
                        DateTimeOffset.UtcNow))
                    .ToList());

        public Task UpsertAuthorizedTrafficAgencyAsync(
            Guid tenantId,
            Guid trafficAgencyId,
            bool isEnabled,
            Guid actorUserId,
            CancellationToken ct = default)
        {
            _otMatrix[(tenantId, trafficAgencyId)] = isEnabled;
            return Task.CompletedTask;
        }

        public Task<bool?> GetTrafficAgencyEnabledAsync(
            Guid tenantId,
            Guid trafficAgencyId,
            CancellationToken ct = default) =>
            Task.FromResult(_otMatrix.TryGetValue((tenantId, trafficAgencyId), out var enabled)
                ? (bool?)enabled
                : null);
    }

    private sealed class InMemoryModuleConfigRepo : ICompanyModuleConfigsRepository
    {
        private readonly Dictionary<(Guid TenantId, string ModuleKey), CompanyModuleConfig> _rows = [];

        public void SetOnlyOwnVehicles(Guid tenantId, bool onlyOwn)
        {
            var now = DateTimeOffset.UtcNow;
            _rows[(tenantId, CompanyModuleKey.Company)] = CompanyModuleConfig.Create(
                tenantId,
                CompanyModuleKey.Company,
                $$"""{"only_own_vehicles":{{(onlyOwn ? "true" : "false")}}}""",
                ActorId,
                now);
        }

        public Task<IReadOnlyList<CompanyModuleConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompanyModuleConfig>>(_rows.Values.Where(r => r.TenantId == tenantId).ToList());

        public Task<CompanyModuleConfig?> GetByTenantAndModuleAsync(
            Guid tenantId,
            string moduleKey,
            CancellationToken ct = default) =>
            Task.FromResult(_rows.GetValueOrDefault((tenantId, moduleKey)));

        public Task AddAsync(CompanyModuleConfig config, CancellationToken ct = default) => Task.CompletedTask;

        public void Update(CompanyModuleConfig config) { }

        public Task<SignatureWallet?> GetSignatureWalletByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<SignatureWallet?>(null);
    }

    [Fact]
    public async Task EnforceVehicleTenantAccess_OnlyOwnTrue_ExternalPlate_NonExemptUser_Returns403()
    {
        var configRepo = new InMemoryModuleConfigRepo();
        configRepo.SetOnlyOwnVehicles(TenantId, true);
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await EnforceVehicleTenantAccess.HandleAsync(
            new EnforceVehicleTenantAccess.Command(TenantId, ValidUserId, "EXT123"),
            configRepo,
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(VehicleTenantAccessErrorCode.VehicleNotOwned);
        result.Error.ErrorCode.Should().Be("VEHICLE_NOT_OWNED");
    }

    [Fact]
    public async Task EnforceVehicleTenantAccess_OnlyOwnTrue_ExternalPlate_ExemptUser_Allows()
    {
        var configRepo = new InMemoryModuleConfigRepo();
        configRepo.SetOnlyOwnVehicles(TenantId, true);
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await EnforceVehicleTenantAccess.HandleAsync(
            new EnforceVehicleTenantAccess.Command(TenantId, ExemptUserId, "EXT999"),
            configRepo,
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExemptUser.Should().BeTrue();
        result.Value.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EnforceVehicleTenantAccess_OnlyOwnTrue_OwnedPlate_Allows()
    {
        var configRepo = new InMemoryModuleConfigRepo();
        configRepo.SetOnlyOwnVehicles(TenantId, true);
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await EnforceVehicleTenantAccess.HandleAsync(
            new EnforceVehicleTenantAccess.Command(TenantId, ValidUserId, "ABC123"),
            configRepo,
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Allowed.Should().BeTrue();
        result.Value.ExternalPlate.Should().BeFalse();
    }

    [Fact]
    public async Task EnforceVehicleTenantAccess_OnlyOwnFalse_AllowsExternalPlate()
    {
        var configRepo = new InMemoryModuleConfigRepo();
        configRepo.SetOnlyOwnVehicles(TenantId, false);
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await EnforceVehicleTenantAccess.HandleAsync(
            new EnforceVehicleTenantAccess.Command(TenantId, ValidUserId, "EXT001"),
            configRepo,
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.OnlyOwnVehicles.Should().BeFalse();
    }

    [Fact]
    public async Task AssertTrafficAgencyAuthorized_DisabledAgency_Returns403()
    {
        var governanceRepo = new InMemoryGovernanceRepo();
        governanceRepo.SetOtEnabled(TenantId, TrafficAgencyId, false);

        var result = await AssertTrafficAgencyAuthorized.HandleAsync(
            new AssertTrafficAgencyAuthorized.Command(TenantId, TrafficAgencyId),
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(VehicleTenantAccessErrorCode.OtNotAuthorized);
        result.Error.ErrorCode.Should().Be("OT_NOT_AUTHORIZED");
    }

    [Fact]
    public async Task AssertTrafficAgencyAuthorized_EnabledAgency_Allows()
    {
        var governanceRepo = new InMemoryGovernanceRepo();
        governanceRepo.SetOtEnabled(TenantId, TrafficAgencyId, true);

        var result = await AssertTrafficAgencyAuthorized.HandleAsync(
            new AssertTrafficAgencyAuthorized.Command(TenantId, TrafficAgencyId),
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task AssertTrafficAgencyAuthorized_NoMatrixEntry_AllowsByDefault()
    {
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await AssertTrafficAgencyAuthorized.HandleAsync(
            new AssertTrafficAgencyAuthorized.Command(TenantId, TrafficAgencyId),
            governanceRepo,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.MatrixEntryFound.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTenantUserException_InvalidUser_Returns400()
    {
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await AddTenantUserExemption.HandleAsync(
            new AddTenantUserExemption.Command(
                TenantId,
                InvalidUserId,
                "test",
                null,
                ActorId),
            governanceRepo,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantGovernanceErrorCode.InvalidUserId);
    }

    [Fact]
    public async Task CreateTenantUserException_ValidUser_Succeeds()
    {
        var governanceRepo = new InMemoryGovernanceRepo();

        var result = await AddTenantUserExemption.HandleAsync(
            new AddTenantUserExemption.Command(
                TenantId,
                ValidUserId,
                "Operador autorizado",
                null,
                ActorId),
            governanceRepo,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(ValidUserId);
    }
}
