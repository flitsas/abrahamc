using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>Invoca un endpoint del catálogo y registra endpoint_call_log (AC2 #9439).</summary>
public static class InvokeCatalogEndpoint
{
    public sealed record Command(
        Guid TenantId,
        string EndpointCode,
        JsonElement? Payload = null,
        Guid? ProcedureInstanceId = null);

    public sealed record Response(
        string EndpointCode,
        int? HttpStatus,
        bool Succeeded,
        bool RateLimited,
        string? ResponsePreview = null);

    public static async Task<Flit.SharedKernel.Result<Response, EndpointCatalogError>> HandleAsync(
        Command command,
        IEndpointCatalogRepository catalogRepo,
        IEndpointCallLogRepository callLogRepo,
        EndpointInvocationRateLimiter rateLimiter,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct = default)
    {
        var code = command.EndpointCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Flit.SharedKernel.Result<Response, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.Validation, "endpoint_code es obligatorio."));
        }

        if (!rateLimiter.TryAcquire(command.TenantId, code))
        {
            return Flit.SharedKernel.Result<Response, EndpointCatalogError>.Failure(
                new EndpointCatalogError(
                    EndpointCatalogErrorKind.RateLimited,
                    "Rate limit excedido para este endpoint."));
        }

        var entry = await catalogRepo.GetByCodeAsync(command.TenantId, code, ct);
        if (entry is null || !entry.IsActive)
        {
            return Flit.SharedKernel.Result<Response, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.NotFound, "Endpoint no encontrado o inactivo."));
        }

        var requestBody = command.Payload ?? JsonRuleElements.Parse("{}");
        int? httpStatus = null;
        JsonElement? responseBody = null;
        var succeeded = false;

        try
        {
            using var client = httpClientFactory.CreateClient(nameof(InvokeCatalogEndpoint));
            client.Timeout = TimeSpan.FromMilliseconds(Math.Clamp(entry.TimeoutMs, 1000, 120_000));

            using var httpRequest = BuildHttpRequest(entry, requestBody);
            using var httpResponse = await client.SendAsync(httpRequest, ct);
            httpStatus = (int)httpResponse.StatusCode;
            var text = await httpResponse.Content.ReadAsStringAsync(ct);
            responseBody = TryParseJson(text);
            succeeded = httpResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            responseBody = JsonRuleElements.Parse(
                JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message }));
        }

        await callLogRepo.LogAsync(
            new EndpointCallLogEntry(
                command.TenantId,
                code,
                command.ProcedureInstanceId,
                Request: requestBody,
                Response: responseBody,
                HttpStatus: httpStatus,
                Succeeded: succeeded,
                CalledAt: DateTimeOffset.UtcNow),
            ct);

        var preview = responseBody?.GetRawText();
        if (preview is { Length: > 400 })
        {
            preview = preview[..400] + "…";
        }

        return Flit.SharedKernel.Result<Response, EndpointCatalogError>.Success(
            new Response(code, httpStatus, succeeded, RateLimited: false, preview));
    }

    private static HttpRequestMessage BuildHttpRequest(EndpointCatalogRecord entry, JsonElement payload)
    {
        var method = entry.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            ? HttpMethod.Post
            : HttpMethod.Get;

        var request = new HttpRequestMessage(method, entry.Url);

        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(
                payload.GetRawText(),
                Encoding.UTF8,
                "application/json");
        }

        if (!string.Equals(entry.AuthType, "none", StringComparison.OrdinalIgnoreCase) &&
            entry.AuthConfig.TryGetProperty("secret_ref", out var secretRef) &&
            secretRef.ValueKind == JsonValueKind.String)
        {
            request.Headers.TryAddWithoutValidation(
                "X-Flit-Secret-Ref",
                secretRef.GetString());
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static JsonElement? TryParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch
        {
            return JsonRuleElements.Parse(JsonSerializer.Serialize(new { raw = text }));
        }
    }
}
