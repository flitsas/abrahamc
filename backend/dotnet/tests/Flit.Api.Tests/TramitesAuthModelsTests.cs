using Flit.Modules.Identity.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TramitesAuthModelsTests
{
    [Fact]
    public void TramitesLoginResponse_ExposesSessionFields()
    {
        var response = new TramitesLoginResponse(
            UserId: Guid.Parse("01930201-0001-7001-8001-000000000010"),
            Email: "superadmin@flit.com.co",
            TenantId: Guid.Parse("01930101-0001-7001-8001-000000000001"),
            AccountState: "active",
            IsSuperAdmin: true,
            PermissionSlugs: ["modulo.tramites.ver"],
            ExpiresInSeconds: 900);

        response.Email.Should().Be("superadmin@flit.com.co");
        response.IsSuperAdmin.Should().BeTrue();
        response.PermissionSlugs.Should().Contain("modulo.tramites.ver");
    }
}
