using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Flit.Api.Endpoints;
using Flit.Api.Middleware;
using Flit.Api.OpenApi;
using Flit.Infrastructure;
using Flit.Infrastructure.MultiTenant;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Notifications;
using Flit.Modules.Notifications.Ports;
using Flit.Modules.Identity.Adapters;
using Flit.Modules.Identity.Ports;
using Flit.Modules.Integrations.Adapters;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.SharedKernel;
using Flit.SharedKernel.Pdf;
using Flit.Infrastructure.Configuration;

// FLIT 2.0 — base limpia post-reset trámites (2026-06).
// Superficie API: health, users, RBAC, auth, menú/permisos del usuario actual.

FlitEnvFileLoader.LoadIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "core-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Services.AddFlitNotifications();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddFlitPdf();

var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddSingleton<IUsuariosRepository, InMemoryUsuariosRepository>();
builder.Services.AddSingleton<ICredentialsRepository, InMemoryCredentialsRepository>();
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

var coreConnStr = builder.Configuration.GetConnectionString("Core")
    ?? builder.Configuration.GetConnectionString("FlitDb");
var usePostgres = !string.IsNullOrWhiteSpace(coreConnStr);
if (usePostgres)
{
    builder.Services.AddPostgresInfrastructure(
        coreConnStr!, builder.Configuration, builder.Environment);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<Flit.Modules.Companies.Ports.ICompaniesSessionContext,
        Flit.Api.Services.HttpCompaniesSessionContext>();
}
else if (builder.Configuration.GetValue("Procedures:RequirePostgreSQL", true))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Core (PostgreSQL) es obligatoria para trámites y parametrización. " +
        "Levante Postgres (infra/docker-compose.yml), ejecute «pnpm migrate» y reinicie core-api.");
}

builder.Services.AddSingleton<Flit.Modules.Users.Ports.ICognitoDirectory,
    Flit.Modules.Users.Adapters.StubCognitoDirectory>();
builder.Services.AddSingleton<Flit.Modules.Users.Application.IPasswordGenerator,
    Flit.Modules.Users.Adapters.TempPasswordGenerator>();
if (!usePostgres)
{
    builder.Services.AddSingleton<Flit.Modules.Users.Ports.IUsersRepository,
        Flit.Modules.Users.Adapters.InMemoryUsersRepository>();
    builder.Services.AddSingleton<Flit.Modules.Users.Application.IUnitOfWork,
        Flit.Modules.Users.Adapters.InMemoryUnitOfWork>();
}

builder.Services.AddSingleton<Flit.Modules.Auth.Domain.Aes256GcmSecretCipher>(sp =>
{
    var keyB64 = builder.Configuration["Mfa:MasterKeyBase64"];
    var keyId = builder.Configuration["Mfa:KeyId"] ?? "v1";
    if (!string.IsNullOrWhiteSpace(keyB64))
        return Flit.Modules.Auth.Domain.Aes256GcmSecretCipher.FromEnv(keyB64, keyId);
    var ephemeral = new byte[32];
    System.Security.Cryptography.RandomNumberGenerator.Fill(ephemeral);
    Log.Warning("⚠  MFA_MASTER_KEY_BASE64 no configurada — usando key efimera (DEV ONLY)");
    return new Flit.Modules.Auth.Domain.Aes256GcmSecretCipher(ephemeral, $"{keyId}-ephemeral");
});
builder.Services.AddSingleton<Flit.Modules.Auth.Domain.TotpService>();
builder.Services.AddSingleton<Flit.Modules.Auth.Ports.IMfaSessionStore,
    Flit.Modules.Auth.Adapters.InMemoryMfaSessionStore>();
if (!usePostgres)
{
    builder.Services.AddSingleton<Flit.Modules.Auth.Application.IPasswordResetTokensRepository,
        Flit.Modules.Auth.Adapters.InMemoryPasswordResetTokensRepository>();
}

builder.Services.AddSingleton<Flit.Modules.Rbac.Ports.IPermissionsCache,
    Flit.Modules.Rbac.Adapters.InMemoryPermissionsCache>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ExternalQueryCircuitBreaker>();

var verifikToken = builder.Configuration["Verifik:ApiToken"]
    ?? Environment.GetEnvironmentVariable("VERIFIK_API_TOKEN");
var verifikOptions = new VerifikOptions
{
    BaseUrl = builder.Configuration["Verifik:BaseUrl"] ?? "https://api.verifik.co",
    ApiToken = verifikToken ?? string.Empty,
    AuthScheme = builder.Configuration["Verifik:AuthScheme"] ?? "Bearer",
    TimeoutSeconds = builder.Configuration.GetValue("Verifik:TimeoutSeconds", 30),
};
builder.Services.AddSingleton(verifikOptions);

builder.Services.AddHttpClient(nameof(VerifikHttpExternalQueryProvider), client =>
{
    client.Timeout = TimeSpan.FromSeconds(verifikOptions.TimeoutSeconds);
});

if (!string.IsNullOrWhiteSpace(verifikOptions.ApiToken))
{
    builder.Services.AddSingleton<IExternalQueryProvider, VerifikHttpExternalQueryProvider>();
    Log.Information("Consultas externas: Verifik HTTP (token configurado).");
}
else
{
    builder.Services.AddSingleton<IExternalQueryProvider, MockExternalQueryProvider>();
    Log.Warning("Verifik:ApiToken no configurado — consultas externas en modo MOCK. Configure VERIFIK_API_TOKEN o Verifik:ApiToken.");
}
if (!usePostgres)
{
    builder.Services.AddSingleton<InMemoryExternalQueryCallLogRepository>();
    builder.Services.AddSingleton<IExternalQueryCallLogRepository>(sp =>
        sp.GetRequiredService<InMemoryExternalQueryCallLogRepository>());
    builder.Services.AddSingleton<InMemoryRuntSyncLogRepository>();
    builder.Services.AddSingleton<IRuntSyncLogRepository>(sp =>
        sp.GetRequiredService<InMemoryRuntSyncLogRepository>());
}

builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Application.EndpointInvocationRateLimiter>();
builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesCatalogRepository>();
builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesRepository>(sp =>
    new Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesRepository(
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesCatalogRepository>()));
builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryEndpointCatalogRepository>();
builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryEndpointCallLogRepository>();
builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryRuleExecutionLogRepository>();

if (!usePostgres)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<Flit.Modules.Companies.Ports.ICompaniesSessionContext,
        Flit.Api.Services.HttpCompaniesSessionContext>();
    builder.Services.AddScoped<Flit.Infrastructure.MultiTenant.ITenantContext, TenantContext>();
    builder.Services.AddSingleton<Flit.SharedKernel.ITenantContext, AmbientTenantContext>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Application.IInvitationTokenHasher,
        Flit.Modules.IdentityVerification.Application.Sha256InvitationTokenHasher>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Application.IInvitationTokenGenerator,
        Flit.Modules.IdentityVerification.Application.SecureInvitationTokenGenerator>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureCatalogLookup,
        Flit.Modules.IdentityVerification.Adapters.InMemoryIdSecureCatalogLookup>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationActivationRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationInvitationRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationSessionRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationSessionStepRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationEvidenceRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureFileRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationOcrResultRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationAiVerdictRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureDocumentOcrProvider,
        Flit.Modules.IdentityVerification.Adapters.MockIdSecureDocumentOcrProvider>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureBiometricEvaluator,
        Flit.Modules.IdentityVerification.Adapters.MockIdSecureBiometricEvaluator>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureCrossMatchEvaluator,
        Flit.Modules.IdentityVerification.Adapters.DocumentNumberCrossMatchEvaluator>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IProcedureIdentityAdvanceNotifier,
        Flit.Modules.IdentityVerification.Adapters.CaptureProcedureIdentityAdvanceNotifier>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IProcedureIdentityValidationRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationManualOverrideRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureReviewReadRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationStore>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationAuditRepository>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationAuditRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationAuditRepository>());
    builder.Services.AddIdSecureBlobStorage(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryDataAccessLogRepository>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IDataAccessLogRepository>(
        sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryDataAccessLogRepository>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IInvitationEmailSender,
        Flit.Modules.IdentityVerification.Adapters.CaptureInvitationEmailSender>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecurePublicUrlProvider>(
        new Flit.Modules.IdentityVerification.Adapters.ConfigIdSecurePublicUrlProvider("http://localhost:4001"));
    builder.Services.AddScoped<Flit.Modules.IdentityVerification.Ports.ITramiteCreatedConsumer,
        Flit.Modules.IdentityVerification.Adapters.TramiteCreatedConsumer>();

    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureRulesRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureRulesCatalogRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureRulesCatalogRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IEndpointCatalogRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryEndpointCatalogRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IEndpointCallLogRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryEndpointCallLogRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IRuleExecutionLogRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryRuleExecutionLogRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IRuleEndpointInvoker>(sp =>
        new Flit.Modules.ProceduresConfig.Application.CatalogRuleEndpointInvoker(
            sp.GetRequiredService<Flit.Modules.ProceduresConfig.Ports.IEndpointCatalogRepository>(),
            sp.GetRequiredService<Flit.Modules.ProceduresConfig.Ports.IEndpointCallLogRepository>(),
            sp.GetRequiredService<Flit.Modules.ProceduresConfig.Application.EndpointInvocationRateLimiter>(),
            sp.GetRequiredService<IHttpClientFactory>()));
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProceduresConfigReadRepository>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProceduresConfigReadRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProceduresConfigReadRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IDocumentTypesReadRepository,
        Flit.Modules.ProceduresConfig.Adapters.InMemoryDocumentTypesReadRepository>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureFilingAuditPort>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureFilingAuditPort>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureFilingAuditPort>());

    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryDocumentTemplateRepository>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IDocumentTemplateRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryDocumentTemplateRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureDocumentRepository>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureDocumentRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedureDocumentRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedurePdfFileStore>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedurePdfFileStore>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryProcedurePdfFileStore>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Adapters.InMemoryOtConsolidatedDocOrderRepository>();
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IOtConsolidatedDocOrderRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.ProceduresConfig.Adapters.InMemoryOtConsolidatedDocOrderRepository>());

    // TRA-02 #9434 — repositorio de instancias en memoria
    builder.Services.AddSingleton<Flit.Modules.Procedures.Adapters.InMemoryProcedureInstanceRepository>();
    builder.Services.AddSingleton<Flit.Modules.Procedures.Domain.IProcedureInstanceRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.Procedures.Adapters.InMemoryProcedureInstanceRepository>());
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureInstanceExistsPort>(sp =>
        new Flit.Modules.Procedures.Adapters.ProcedureInstanceExistsAdapter(
            sp.GetRequiredService<Flit.Modules.Procedures.Domain.IProcedureInstanceRepository>()));
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IProcedureInstanceTrafficAgencyPort>(sp =>
        new Flit.Modules.Procedures.Adapters.ProcedureInstanceTrafficAgencyAdapter(
            sp.GetRequiredService<Flit.Modules.Procedures.Domain.IProcedureInstanceRepository>()));
    builder.Services.AddSingleton<Flit.Modules.ProceduresConfig.Ports.IApprovedIdentityEvidencePort>(sp =>
        new Flit.Modules.IdentityVerification.Adapters.ApprovedIdentityEvidenceAdapter(
            sp.GetRequiredService<Flit.Modules.IdentityVerification.Ports.IProcedureIdentityValidationRepository>(),
            sp.GetRequiredService<Flit.Modules.IdentityVerification.Ports.IVerificationEvidenceRepository>(),
            sp.GetRequiredService<Flit.Modules.IdentityVerification.Ports.IIdSecureFileRepository>(),
            sp.GetService<Flit.Modules.IdentityVerification.Ports.IIdSecureBlobStorage>()));

    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationProvider,
        Flit.Modules.IdentityVerification.Adapters.MockIdentityVerificationProvider>();

    builder.Services.AddSingleton<Flit.Modules.Rbac.Adapters.InMemoryRoleAssignmentsRepository>();
    builder.Services.AddSingleton<Flit.Modules.Rbac.Ports.IRoleAssignmentsRepository>(
        sp => sp.GetRequiredService<Flit.Modules.Rbac.Adapters.InMemoryRoleAssignmentsRepository>());
    builder.Services.AddSingleton<Flit.Modules.Rbac.Ports.IRolesRepository,
        Flit.Modules.Rbac.Adapters.InMemoryRolesRepository>();
    builder.Services.AddSingleton<Flit.Modules.Rbac.Ports.IPermissionsRepository,
        Flit.Modules.Rbac.Adapters.InMemoryPermissionsRepository>();
    builder.Services.AddSingleton<Flit.Modules.Rbac.Ports.IMenuItemsRepository,
        Flit.Modules.Rbac.Adapters.InMemoryMenuItemsRepository>();
}

// TRA-03 #9435 — repositorios in-memory solo sin PostgreSQL (con PG usa EF en InfrastructureExtensions).
if (!usePostgres)
{
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryVerificationSessionRepository>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IVerificationSessionRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryVerificationSessionRepository>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryProcedureIdentityValidationRepository>();
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IProcedureIdentityValidationRepository>(sp =>
        sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryProcedureIdentityValidationRepository>());
    builder.Services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationProvider,
        Flit.Modules.IdentityVerification.Adapters.MockIdentityVerificationProvider>();
}

builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
var identityProviderName = builder.Configuration["Identity:Provider"] ?? "Local";
if (string.Equals(identityProviderName, "Cognito", StringComparison.OrdinalIgnoreCase))
{
    var cognitoSettings = new CognitoSettings(
        UserPoolId: builder.Configuration["Cognito:UserPoolId"]
            ?? throw new InvalidOperationException("Cognito:UserPoolId requerido cuando Identity:Provider=Cognito"),
        AppClientId: builder.Configuration["Cognito:AppClientId"]
            ?? throw new InvalidOperationException("Cognito:AppClientId requerido"),
        AppClientSecret: builder.Configuration["Cognito:AppClientSecret"] ?? string.Empty);
    builder.Services.AddSingleton(cognitoSettings);
    builder.Services.AddSingleton<Amazon.CognitoIdentityProvider.IAmazonCognitoIdentityProvider>(sp =>
    {
        var region = builder.Configuration["AWS:Region"] ?? "us-east-1";
        var accessKey = builder.Configuration["AWS:AccessKeyId"];
        var secretKey = builder.Configuration["AWS:SecretAccessKey"];
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new Amazon.CognitoIdentityProvider.AmazonCognitoIdentityProviderClient(
                accessKey, secretKey, regionEndpoint)
            : new Amazon.CognitoIdentityProvider.AmazonCognitoIdentityProviderClient(regionEndpoint);
    });
    builder.Services.AddSingleton<IIdentityProvider, CognitoIdentityProvider>();
}
else
{
    builder.Services.AddScoped<IIdentityProvider, LocalIdentityProvider>();
}

builder.Services.AddSingleton<ITokenIssuer>(sp =>
{
    var clock = sp.GetRequiredService<IClock>();
    var config = sp.GetRequiredService<IConfiguration>();
    var contentRoot = sp.GetRequiredService<IHostEnvironment>().ContentRootPath;
    var settings = LoadJwtSettings(config, contentRoot);
    return new RsaJwtTokenIssuer(settings, clock);
});

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi("v1", options =>
{
    FlitOpenApiSecurityTransformers.Configure(options);

    options.AddDocumentTransformer((document, context, _) =>
    {
        document.Info = new()
        {
            Title = "FLIT Core API",
            Version = "v1",
            Description =
                "Contrato REST core-api (Trámites 2.0). Spec canónica versionada: docs/openapi.yaml",
        };

        var httpContext = context.ApplicationServices
            .GetRequiredService<IHttpContextAccessor>()
            .HttpContext;
        if (httpContext is not null)
        {
            var request = httpContext.Request;
            var scheme = request.Headers[ForwardedHeadersDefaults.XForwardedProtoHeaderName]
                .FirstOrDefault() ?? request.Scheme;
            var host = request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName]
                .FirstOrDefault() ?? request.Host.Value;
            if (!string.IsNullOrEmpty(host)
                && host.Contains("flitsas.online", StringComparison.OrdinalIgnoreCase)
                && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "https";
            }

            document.Servers = [new() { Url = $"{scheme}://{host}/" }];
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (!string.IsNullOrEmpty(coreConnStr))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<FlitDbContext>();
    await db.Database.MigrateAsync();
    Log.Information(
        "✓ Migraciones EF Core aplicadas correctamente ({Environment})",
        app.Environment.EnvironmentName);
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Core es requerida. Las migraciones EF Core deben aplicarse al arrancar en Dev, QA y PDN.");
}

if (usePostgres && app.Environment.IsDevelopment())
{
    var blobStorage = app.Services.GetRequiredService<Flit.Modules.IdentityVerification.Ports.IIdSecureBlobStorage>();
    var blobMode = blobStorage is Flit.Infrastructure.Adapters.MinioIdSecureBlobStorage
        ? $"MinIO ({app.Configuration["MinIO:Endpoint"]})"
        : "IN-MEMORY (evidencias no persisten en disco)";
    Log.Information("IDSecure blob storage: {Mode}", blobMode);

    var smtpHost = app.Configuration["Smtp:Host"];
    var hasRealSmtp = !string.IsNullOrWhiteSpace(smtpHost)
                      && smtpHost is not ("localhost" or "127.0.0.1");
    var useDevCapture = app.Configuration.GetValue("IdSecure:Smtp:UseDevCapture", false);
    var mode = useDevCapture && !hasRealSmtp
        ? "DEV (enlaces en consola)"
        : $"SMTP ({smtpHost ?? "no configurado"})";
    Log.Information("IDSecure email sender: {Mode}", mode);
}

app.UseCors();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();

if (usePostgres)
{
    app.UseMiddleware<TramitesSessionEpochMiddleware>();
}

if (usePostgres)
{
    app.UseMiddleware<Flit.Api.Middleware.CompaniesSessionAmbientMiddleware>();
}

app.MapGet("/api/v1/health", () => new HealthResponse(
    Status: "ok",
    Service: "core-api",
    Version: "2.0.0-flit-shell"
))
.WithName("Health")
.WithTags("System");

app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/api/v1/health"));

// Shell legacy (Cognito / rbac schema rbac.*) — reemplazado por Trámites 2.0 identity.* (#9417).
// app.MapUsersEndpoints();
// app.MapRbacEndpoints();
// app.MapAuthEndpoints();
if (usePostgres)
{
    app.MapTramitesAuthEndpoints();
    app.MapTramitesRbacEndpoints();
    app.MapTramitesOnboardingEndpoints();
    app.MapTramitesUsersEndpoints();
    app.MapTramitesAdminEndpoints();
    app.MapTramitesProfileEndpoints();
    app.MapTramitesSupportEndpoints();
    app.MapCompaniesEndpoints();
    app.MapRuntEndpoints();
    app.MapIntegrationsEndpoints();
    app.MapOtEndpoints();
    app.MapOtRulesEndpoints();
    app.MapOtConsolidatedOrderEndpoints();
    app.MapOtDocumentLabelsEndpoints();
}
app.MapProceduresConfigEndpoints();
app.MapProceduresConfigAdminEndpoints();
app.MapDocumentTemplatesEndpoints();
app.MapProceduresEndpoints();
app.MapProcedureInstancesEndpoints();
app.MapIdentityVerificationEndpoints();
// Legacy Cognito auth — reemplazado por TramitesAuthEndpoints (#9415) cuando usePostgres.
// app.MapAuthEndpoints();
app.MapIdSecureInternalEndpoints();
app.MapProcedureIdentityWebhookEndpoints();
app.MapIdSecurePublicEndpoints();
app.MapIdSecureBackofficeEndpoints();
app.MapIdSecureAnalyticsEndpoints();
app.MapIdSecureHabeasDataEndpoints();
app.MapIdSecureOperatorEndpoints();
app.MapDevSeedEndpoints(app.Environment);
app.MapFlitNotificationsHub();

if (ShouldExposeApiDocs(app.Environment))
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FLIT Core API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "FLIT Core API — Swagger";
        options.ConfigObject.PersistAuthorization = true;
    });
}

app.Run();

static bool ShouldExposeApiDocs(IHostEnvironment env) =>
    !env.IsProduction() && !env.IsEnvironment("PDN");

static bool TryReadJwtKeyFiles(
    string contentRoot,
    string? privateKeyPath,
    string? publicKeyPath,
    out string privatePem,
    out string publicPem)
{
    privatePem = string.Empty;
    publicPem = string.Empty;
    if (string.IsNullOrWhiteSpace(privateKeyPath) || string.IsNullOrWhiteSpace(publicKeyPath))
        return false;

    var privFull = Path.IsPathRooted(privateKeyPath)
        ? privateKeyPath
        : Path.GetFullPath(Path.Combine(contentRoot, privateKeyPath));
    var pubFull = Path.IsPathRooted(publicKeyPath)
        ? publicKeyPath
        : Path.GetFullPath(Path.Combine(contentRoot, publicKeyPath));

    if (!File.Exists(privFull) || !File.Exists(pubFull))
        return false;

    privatePem = File.ReadAllText(privFull);
    publicPem = File.ReadAllText(pubFull);
    return true;
}

static JwtSettings LoadJwtSettings(IConfiguration config, string contentRoot)
{
    var issuer = config["Jwt:Issuer"] ?? "tramites-core";
    var audience = config["Jwt:Audience"] ?? "tramites-internal";
    var accessTtlMin = int.TryParse(config["Jwt:AccessTokenMinutes"], out var aTtl) ? aTtl : 15;
    var refreshTtlDays = int.TryParse(config["Jwt:RefreshTokenDays"], out var rTtl) ? rTtl : 7;
    var devGenerate = string.Equals(
        config["Jwt:DevGenerate"], "true", StringComparison.OrdinalIgnoreCase);

    string privatePem, publicPem;

    if (devGenerate)
    {
        var privPath = config["Jwt:PrivateKeyPath"];
        var pubPath = config["Jwt:PublicKeyPath"];
        if (TryReadJwtKeyFiles(contentRoot, privPath, pubPath, out privatePem, out publicPem))
            return new JwtSettings(
                PrivateKeyPem: privatePem,
                PublicKeyPem: publicPem,
                Issuer: issuer,
                Audience: audience,
                AccessTtl: TimeSpan.FromMinutes(accessTtlMin),
                RefreshTtl: TimeSpan.FromDays(refreshTtlDays));

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        privatePem = rsa.ExportRSAPrivateKeyPem();
        publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        Log.Warning(
            "JWT DevGenerate: llaves efimeras (DEV). Ejecuta ./scripts/gen-secrets.sh para llaves estables.");
    }
    else
    {
        var privPath = config["Jwt:PrivateKeyPath"] ?? "/run/secrets/jwt_private";
        var pubPath = config["Jwt:PublicKeyPath"] ?? "/run/secrets/jwt_public";
        if (!TryReadJwtKeyFiles(contentRoot, privPath, pubPath, out privatePem, out publicPem))
            throw new FileNotFoundException(
                $"JWT keys no encontradas. Paths: {privPath}, {pubPath}. " +
                "Ejecuta ./scripts/gen-secrets.sh o Jwt:DevGenerate=true.");
    }

    return new JwtSettings(
        PrivateKeyPem: privatePem,
        PublicKeyPem: publicPem,
        Issuer: issuer,
        Audience: audience,
        AccessTtl: TimeSpan.FromMinutes(accessTtlMin),
        RefreshTtl: TimeSpan.FromDays(refreshTtlDays));
}

internal sealed record HealthResponse(string Status, string Service, string Version);

public partial class Program;
