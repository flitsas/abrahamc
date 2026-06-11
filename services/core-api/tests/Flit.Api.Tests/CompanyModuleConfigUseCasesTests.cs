using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class CompanyModuleConfigUseCasesTests
{
    private sealed class InMemoryModuleConfigRepo : ICompanyModuleConfigsRepository
    {
        private readonly List<CompanyModuleConfig> _rows = [];

        public Task<IReadOnlyList<CompanyModuleConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompanyModuleConfig>>(_rows.Where(r => r.TenantId == tenantId).ToList());

        public Task<CompanyModuleConfig?> GetByTenantAndModuleAsync(
            Guid tenantId, string moduleKey, CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(r => r.TenantId == tenantId && r.ModuleKey == moduleKey));

        public Task AddAsync(CompanyModuleConfig config, CancellationToken ct = default)
        {
            _rows.Add(config);
            return Task.CompletedTask;
        }

        public void Update(CompanyModuleConfig config) { }

        public Task<SignatureWallet?> GetSignatureWalletByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<SignatureWallet?>(null);
    }

    private static readonly Guid TenantId = Guid.Parse("01930110-0001-7001-8001-000000000001");
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Upsert_NormalizesMatriculaAlias_ToRegistration()
    {
        var repo = new InMemoryModuleConfigRepo();
        var clock = new FakeClock();

        var result = await UpsertCompanyModuleConfig.HandleAsync(
            new UpsertCompanyModuleConfig.Command(
                TenantId,
                "matricula",
                """{"default_ot_code":"OT-BOGOTA"}""",
                true,
                ActorId),
            repo,
            _ => Task.FromResult(0),
            clock,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ModuleKey.Should().Be(CompanyModuleKey.Registration);
    }

    [Fact]
    public async Task Upsert_RecaudoWithoutPaymentMethod_Returns400Schema()
    {
        var repo = new InMemoryModuleConfigRepo();
        var clock = new FakeClock();

        var result = await UpsertCompanyModuleConfig.HandleAsync(
            new UpsertCompanyModuleConfig.Command(
                TenantId,
                CompanyModuleKey.Recaudo,
                """{"currency":"COP"}""",
                true,
                ActorId),
            repo,
            _ => Task.FromResult(0),
            clock,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UpsertModuleConfigErrorCode.InvalidConfigSchema);
    }

    [Fact]
    public async Task Get_MasksSmtpPassword_InCompanyConfig()
    {
        var repo = new InMemoryModuleConfigRepo();
        var clock = new FakeClock();
        var json = """{"smtp_settings":{"host":"smtp.test","password":"secret123"}}""";

        await UpsertCompanyModuleConfig.HandleAsync(
            new UpsertCompanyModuleConfig.Command(TenantId, CompanyModuleKey.Company, json, true, ActorId),
            repo,
            _ => Task.FromResult(0),
            clock,
            TestContext.Current.CancellationToken);

        var row = await GetCompanyModuleConfig.HandleAsync(
            new GetCompanyModuleConfig.Query(TenantId, CompanyModuleKey.Company),
            repo,
            TestContext.Current.CancellationToken);

        row.Should().NotBeNull();
        row!.ConfigJson.Should().Contain("\"password\":\"***\"");
        row.ConfigJson.Should().NotContain("secret123");
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    }
}
