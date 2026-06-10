using Flit.Infrastructure.MultiTenant;

namespace Flit.Api.Extensions;

public static class TenantMismatchResultExtensions
{
    public static IResult? ToResult(this TenantMismatchError? error) =>
        error is null
            ? null
            : Results.Json(
                new { error = error.Code, message = error.Message },
                statusCode: StatusCodes.Status403Forbidden);
}
