using Flit.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Flit.Api.OpenApi;

/// <summary>
/// Expone bearerAuth + sessionCookie en /openapi/v1.json para Swagger UI (QA).
/// Alineado con docs/openapi.yaml — Feature #9549/#9550.
/// </summary>
internal static class FlitOpenApiSecurityTransformers
{
    public const string BearerSchemeId = "bearerAuth";
    public const string CookieSchemeId = "sessionCookie";

    private static readonly string[] PublicPathPrefixes =
    [
        "/api/v1/health",
        "/public/idsecure",
        "/api/v1/integrations/webhooks/",
        "/api/v1/dev/",
    ];

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer(ApplySecuritySchemesAsync);
        options.AddOperationTransformer(ApplyOperationSecurityAsync);
    }

    private static Task ApplySecuritySchemesAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext _,
        CancellationToken __)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[BearerSchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Access token JWT (campo accessToken de POST /api/v1/auth/login). " +
                "Swagger lo envía como Authorization: Bearer &lt;token&gt;.",
        };

        document.Components.SecuritySchemes[CookieSchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthCookieNames.Access,
            Description =
                $"Cookie de sesión del SPA (`{AuthCookieNames.Access}`). " +
                "Alternativa al header Bearer cuando se prueba desde el navegador.",
        };

        return Task.CompletedTask;
    }

    private static Task ApplyOperationSecurityAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken _)
    {
        if (IsPublicEndpoint(context))
        {
            operation.Security = [];
            return Task.CompletedTask;
        }

        var document = context.Document;
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = [],
            },
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(CookieSchemeId, document)] = [],
            },
        ];

        return Task.CompletedTask;
    }

    private static bool IsPublicEndpoint(OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAllowAnonymous>().Any()
            || metadata.OfType<AllowAnonymousAttribute>().Any())
        {
            return true;
        }

        var relativePath = context.Description.RelativePath;
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var normalized = "/" + relativePath.TrimStart('/');
        return PublicPathPrefixes.Any(prefix =>
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
