using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class RuntVehicleQueryUseCasesTests
{
    private static readonly Guid TenantId = Guid.Parse("01930110-0001-7001-8001-000000000001");
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    [Fact]
    public void BuildAttemptChain_DefaultPolicy_UsesVerifikThenIntempoWithoutRunt()
    {
        var chain = RuntContingencyPolicy.Default.BuildAttemptChain();

        chain.Should().Equal(RuntProviderCode.Verifik, RuntProviderCode.Intempo);
    }

    [Fact]
    public async Task HandleAsync_ValidPlate_UsesVerifikAndLogsSuccess()
    {
        var harness = new TestHarness();
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Verifik,
            _ => Success("verifik-stub")));

        var result = await ExecuteAsync(harness, "ABC123");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProviderUsed.Should().Be(RuntProviderCode.Verifik);
        result.Value.Outcome.Should().Be(RuntQueryOutcome.Success);
        result.Value.FailoverFrom.Should().BeNull();

        harness.SyncLogs.Should().ContainSingle(l =>
            l.Provider == RuntProviderCode.Verifik && l.Outcome == RuntQueryOutcome.Ok);
    }

    [Fact]
    public async Task HandleAsync_VerifikTimeout_FailoversToIntempoWithReason()
    {
        var harness = new TestHarness();
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Verifik,
            req => req.SimulateRuntUnavailable
                ? Fail(RuntQueryOutcome.Timeout)
                : Success("verifik-stub")));
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Intempo,
            _ => Success("intempo-stub")));

        var result = await ExecuteAsync(harness, "ABC123", simulateRuntUnavailable: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProviderUsed.Should().Be(RuntProviderCode.Intempo);
        result.Value.FailoverFrom.Should().Be(RuntProviderCode.Verifik);
        result.Value.ResultJson.Should().Contain("intempo-stub");

        var successLog = harness.SyncLogs.Single(l =>
            l.Provider == RuntProviderCode.Intempo && l.Outcome == RuntQueryOutcome.Ok);
        successLog.PayloadJson.Should().Contain("\"reason\":\"failover\"");
        successLog.FailoverFrom.Should().Be(RuntProviderCode.Verifik);

        harness.SyncLogs.Should().Contain(l =>
            l.Provider == RuntProviderCode.Verifik && l.Outcome == RuntQueryOutcome.Timeout);
    }

    [Fact]
    public async Task HandleAsync_PlateXyz000_ReturnsVehicleNotFound()
    {
        var harness = new TestHarness();
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Verifik,
            req => req.Plate == "XYZ000"
                ? Fail(RuntQueryOutcome.NotFound)
                : Success("verifik-stub")));
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Intempo,
            req => req.Plate == "XYZ000"
                ? Fail(RuntQueryOutcome.NotFound)
                : Success("intempo-stub")));

        var result = await ExecuteAsync(harness, "XYZ000");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(RuntVehicleQueryErrorCode.VehicleNotFound);
        harness.SyncLogs.Should().Contain(l =>
            l.Provider == RuntProviderCode.Verifik && l.Outcome == RuntQueryOutcome.Failed);
        harness.SyncLogs.Should().Contain(l =>
            l.Provider == RuntProviderCode.Intempo && l.Outcome == RuntQueryOutcome.Failed);
    }

    [Fact]
    public async Task HandleAsync_AllProvidersDown_ReturnsRuntUnavailable()
    {
        var harness = new TestHarness();
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Verifik,
            _ => Fail(RuntQueryOutcome.Failed)));
        harness.AddProvider(new FakeProvider(
            RuntProviderCode.Intempo,
            _ => Fail(RuntQueryOutcome.Failed)));

        var result = await ExecuteAsync(harness, "ABC123", simulateAllProvidersDown: true);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(RuntVehicleQueryErrorCode.AllProvidersUnavailable);
    }

    private static Task<Result<QueryVehicleWithRuntContingency.Response, RuntVehicleQueryError>> ExecuteAsync(
        TestHarness harness,
        string plate,
        bool simulateRuntUnavailable = false,
        bool simulateAllProvidersDown = false) =>
        QueryVehicleWithRuntContingency.HandleAsync(
            new QueryVehicleWithRuntContingency.Command(
                TenantId,
                plate,
                ProcedureInstanceId: null,
                ActorId,
                simulateRuntUnavailable,
                simulateAllProvidersDown),
            harness.ConfigRepo,
            harness.Providers,
            harness.SyncLogRepo,
            harness.QueryResultsRepo,
            harness.CircuitBreaker,
            _ => Task.CompletedTask,
            harness.Clock,
            TestContext.Current.CancellationToken);

    private static RuntVehicleQueryAttemptResult Success(string mockProvider) =>
        new(true, $$"""{"mock_provider":"{{mockProvider}}"}""", RuntQueryOutcome.Ok, null);

    private static RuntVehicleQueryAttemptResult Fail(string outcome) =>
        new(false, """{"error":"provider_failed"}""", outcome, "provider_failed");

    private sealed class FakeProvider(
        string code,
        Func<RuntVehicleQueryRequest, RuntVehicleQueryAttemptResult> handler) : IRuntVehicleQueryProvider
    {
        public string ProviderCode { get; } = code;

        public Task<RuntVehicleQueryAttemptResult> QueryVehicleAsync(
            RuntVehicleQueryRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(handler(request));
    }

    private sealed class TestHarness
    {
        public InMemoryModuleConfigRepo ConfigRepo { get; } = new();
        public InMemorySyncLogRepo SyncLogRepo { get; } = new();
        public InMemoryQueryResultsRepo QueryResultsRepo { get; } = new();
        public NoOpCircuitBreaker CircuitBreaker { get; } = new();
        public FakeClock Clock { get; } = new();
        public List<IRuntVehicleQueryProvider> Providers { get; } = [];

        public IReadOnlyList<RuntSyncLogEntry> SyncLogs => SyncLogRepo.Entries;

        public void AddProvider(IRuntVehicleQueryProvider provider) => Providers.Add(provider);
    }

    private sealed class InMemoryModuleConfigRepo : ICompanyModuleConfigsRepository
    {
        public Task<IReadOnlyList<CompanyModuleConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompanyModuleConfig>>([]);

        public Task<CompanyModuleConfig?> GetByTenantAndModuleAsync(
            Guid tenantId, string moduleKey, CancellationToken ct = default) =>
            Task.FromResult<CompanyModuleConfig?>(null);

        public Task AddAsync(CompanyModuleConfig config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Update(CompanyModuleConfig config) { }

        public Task<SignatureWallet?> GetSignatureWalletByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<SignatureWallet?>(null);
    }

    private sealed class InMemorySyncLogRepo : IRuntSyncLogRepository
    {
        public List<RuntSyncLogEntry> Entries { get; } = [];

        public Task AddAsync(RuntSyncLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryQueryResultsRepo : IProcedureQueryResultsRepository
    {
        public Task AddAsync(ProcedureQueryResultSnapshot snapshot, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpCircuitBreaker : IRuntProviderCircuitBreaker
    {
        public bool IsOpen(Guid tenantId, string providerCode, DateTimeOffset now) => false;

        public void RecordFailure(Guid tenantId, string providerCode, DateTimeOffset now) { }

        public void RecordSuccess(Guid tenantId, string providerCode) { }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    }
}
