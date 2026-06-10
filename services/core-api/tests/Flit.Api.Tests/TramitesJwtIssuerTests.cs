using System.Security.Cryptography;
using Flit.Modules.Identity.Adapters;
using Flit.Modules.Identity.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TramitesJwtIssuerTests : IDisposable
{
    private readonly RsaJwtTokenIssuer _issuer;
    private readonly TestClock _clock = new();

    public TramitesJwtIssuerTests()
    {
        using var rsa = RSA.Create(2048);
        var settings = new JwtSettings(
            PrivateKeyPem: rsa.ExportRSAPrivateKeyPem(),
            PublicKeyPem: rsa.ExportRSAPublicKeyPem(),
            Issuer: "tramites-test",
            Audience: "tramites-internal",
            AccessTtl: TimeSpan.FromMinutes(15),
            RefreshTtl: TimeSpan.FromDays(7));
        _issuer = new RsaJwtTokenIssuer(settings, _clock);
    }

    [Fact]
    public void IssueTramitesAccess_IncludesRolesPermissionsAndEpoch()
    {
        var subject = new TramitesSessionSubject(
            Guid.Parse("01930201-0001-7001-8001-000000000010"),
            Guid.Parse("01930101-0001-7001-8001-000000000001"),
            "user@flit.com.co",
            IsSuperAdmin: false,
            RoleSlugs: ["colaborador", "revisor"],
            PermissionSlugs: ["modulo.tramites.ver", "ui.tramites.crear"],
            PermissionsEpoch: 3);

        var (token, expiresAt) = _issuer.IssueTramitesAccess(subject);

        expiresAt.Should().BeCloseTo(_clock.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(2));

        var validated = _issuer.ValidateTramitesAccess(token);
        validated.Should().NotBeNull();
        validated!.RoleSlugs.Should().BeEquivalentTo(["colaborador", "revisor"]);
        validated.PermissionSlugs.Should().BeEquivalentTo(["modulo.tramites.ver", "ui.tramites.crear"]);
        validated.PermissionsEpoch.Should().Be(3);
    }

    public void Dispose() => _issuer.Dispose();

    private sealed class TestClock : Flit.SharedKernel.IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}
