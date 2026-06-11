using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class CompanyIndexUseCasesTests
{
    [Fact]
    public async Task CreateCompanyIndex_Rejects_When_Not_SuperAdmin()
    {
        var result = await CreateCompanyIndex.HandleAsync(
            new CreateCompanyIndex.Command(
                "900111222-3",
                "Transportes Demo",
                null,
                null,
                null,
                null,
                Guid.NewGuid(),
                IsSuperAdmin: false),
            new FakeTenantProvisioner(),
            new FakeCompaniesRepository(),
            _ => Task.FromResult(1),
            new FakeClock(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CompaniesErrorCode.Forbidden);
    }

    [Fact]
    public async Task CreateCompanyIndex_Returns_409_When_Nit_Already_Exists()
    {
        var provisioner = new FakeTenantProvisioner { NitExists = true };

        var result = await CreateCompanyIndex.HandleAsync(
            new CreateCompanyIndex.Command(
                "900123456-1",
                "Compañía Duplicada",
                null,
                "contacto@demo.com",
                null,
                null,
                Guid.NewGuid(),
                IsSuperAdmin: true),
            provisioner,
            new FakeCompaniesRepository(),
            _ => Task.FromResult(1),
            new FakeClock(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CompaniesErrorCode.NitConflict);
        result.Error.Message.Should().Contain("NIT ya registrado");
    }

    [Fact]
    public async Task CreateCompanyIndex_Creates_Tenant_And_Company_When_Valid()
    {
        var repo = new FakeCompaniesRepository();
        var provisioner = new FakeTenantProvisioner();
        var actorId = Guid.Parse("01930201-0001-7001-8001-000000000010");
        var clock = new FakeClock();

        var result = await CreateCompanyIndex.HandleAsync(
            new CreateCompanyIndex.Command(
                "900555666-7",
                "Transportes Andina Nueva",
                "Andina",
                "admin@andina.com",
                null,
                """{"tramites":true}""",
                actorId,
                IsSuperAdmin: true),
            provisioner,
            repo,
            _ => Task.FromResult(1),
            clock,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nit.Should().Be("900555666-7");
        result.Value.Status.Should().Be("active");
        result.Value.LegalName.Should().Be("Transportes Andina Nueva");
        provisioner.CreatedTenants.Should().HaveCount(1);
        repo.AddedCompanies.Should().HaveCount(1);
        repo.AddedCompanies[0].CreatedBy.Should().Be(actorId);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTenantProvisioner : ICompanyTenantProvisioner
    {
        public bool NitExists { get; set; }
        public List<ProvisionedCompanyTenant> CreatedTenants { get; } = [];

        public Task<bool> NitExistsAsync(string nit, CancellationToken ct = default) =>
            Task.FromResult(NitExists);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<ProvisionedCompanyTenant> CreateAsync(
            string name,
            string nit,
            string slug,
            string? settingsJson,
            Guid createdBy,
            CancellationToken ct = default)
        {
            var tenant = new ProvisionedCompanyTenant(
                Guid.CreateVersion7(),
                name,
                nit,
                slug,
                "active");
            CreatedTenants.Add(tenant);
            return Task.FromResult(tenant);
        }
    }

    private sealed class FakeCompaniesRepository : ICompaniesRepository
    {
        public List<Company> AddedCompanies { get; } = [];

        public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Company?>(null);

        public Task<Company?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<Company?>(null);

        public Task<bool> ExistsForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsByNitAsync(string nit, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task AddAsync(Company company, CancellationToken ct = default)
        {
            AddedCompanies.Add(company);
            return Task.CompletedTask;
        }

        public Task AddModuleConfigAsync(CompanyModuleConfig config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddSignatureWalletAsync(SignatureWallet wallet, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddWalletMovementAsync(SignatureWalletMovement movement, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddVehicleOwnershipRuleAsync(VehicleOwnershipRule rule, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
