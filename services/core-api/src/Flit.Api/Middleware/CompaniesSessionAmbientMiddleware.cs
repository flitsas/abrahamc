using Flit.Modules.Companies.Ports;

namespace Flit.Api.Middleware;

public sealed class CompaniesSessionAmbientMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ICompaniesSessionContext session)
    {
        CompaniesSessionAmbient.Set(session);
        try
        {
            await next(http);
        }
        finally
        {
            CompaniesSessionAmbient.Set(null);
        }
    }
}
