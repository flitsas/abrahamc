using Flit.Modules.ProceduresConfig.Adapters;
using Flit.Modules.ProceduresConfig.Application;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class ProcedureConfigHandlersTests
{
    [Fact]
    public async Task ListActiveProcedureTypes_ReturnsDemoCatalog_ForAndinaTenant()
    {
        var repo = new InMemoryProceduresConfigReadRepository();
        var tenantId = InMemoryProceduresConfigReadRepository.DemoTenantId;

        var types = await ListActiveProcedureTypes.HandleAsync(
            new ListActiveProcedureTypes.Query(tenantId, null),
            repo,
            TestContext.Current.CancellationToken);

        types.Should().NotBeEmpty();
        types.Should().Contain(t => t.Code == "TRA_ESTANDAR");
    }
}
