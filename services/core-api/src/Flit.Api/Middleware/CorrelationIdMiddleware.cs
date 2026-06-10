namespace Flit.Api.Middleware;

/// <summary>
/// Propaga <c>X-Correlation-Id</c> y lo expone en HttpContext para GUC <c>app.request_id</c>.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || !Guid.TryParse(correlationId, out var requestId))
            requestId = Guid.CreateVersion7();

        context.Response.Headers[HeaderName] = requestId.ToString();
        context.Items[ItemKey] = requestId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", requestId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
