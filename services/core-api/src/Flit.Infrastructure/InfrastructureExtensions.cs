using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Flit.Infrastructure.Adapters;
using Flit.Infrastructure.Email;
using Flit.Infrastructure.Integrations;
using Flit.Infrastructure.MultiTenant;
using Flit.Infrastructure.Persistence;
using Flit.Infrastructure.Repositories;
using Flit.Modules.Companies.Ports;

namespace Flit.Infrastructure;

/// <summary>
/// Extension de IServiceCollection para registrar EF Core + repositorios FLIT 2.0.
/// Invocado desde Program.cs cuando ConnectionStrings:Core esta configurado.
///
/// FLIT 2.0:
///   - Identity (Users, PasswordResetTokens, UserAuditLog, SyncInconsistencies)
///   - RBAC (Roles, Permissions, MenuItems, joins)
///   - Notifications (NotificationDelivery — dominio, no senders)
///   - Identity LEGACY (Usuario, IdentityCredential, RefreshTokenEntry) hasta Fase 7
///   - Companies (#9381 CMP-01)
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Registra FlitDbContext (Npgsql) y los repositorios EF Core
    /// disponibles. Los repositorios EF son Scoped (DbContext es Scoped).
    /// </summary>
    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddFlitSmtpEmail(configuration);
        services.AddFlitGlobalEmail(configuration);
        services.AddFlitMultiTenant();
        services.AddScoped<Flit.SharedKernel.ITenantContext, SharedKernelTenantBridge>();
        services.AddScoped<PostgresRlsSession>();
        services.AddScoped<TenantSessionConnectionInterceptor>();

        services.AddDbContext<FlitDbContext>((serviceProvider, opts) =>
            opts.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                })
            .AddInterceptors(
                serviceProvider.GetRequiredService<TenantDbConnectionInterceptor>(),
                serviceProvider.GetRequiredService<TenantSessionConnectionInterceptor>())
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false));

        // ── Users (ADR-0010) ───────────────────────────────────────────
        services.AddScoped<Flit.Modules.Users.Ports.IUsersRepository, EfUsersRepository>();
        services.AddScoped<Flit.Modules.Users.Application.IUnitOfWork, EfUnitOfWork>();

        // ── Auth (Fase 7 — Hybrid Cognito + MFA) ───────────────────────
        services.AddScoped<Flit.Modules.Auth.Application.IPasswordResetTokensRepository,
            EfPasswordResetTokensRepository>();

        // ── RBAC (ADR-0011) ────────────────────────────────────────────
        services.AddScoped<Flit.Modules.Rbac.Ports.IRolesRepository, EfRolesRepository>();
        services.AddScoped<Flit.Modules.Rbac.Ports.IPermissionsRepository, EfPermissionsRepository>();
        services.AddScoped<Flit.Modules.Rbac.Ports.IMenuItemsRepository, EfMenuItemsRepository>();
        services.AddScoped<Flit.Modules.Rbac.Ports.IRoleAssignmentsRepository, EfRoleAssignmentsRepository>();

        // ── Notifications ──────────────────────────────────────────────
        services.AddScoped<Flit.Modules.Notifications.Ports.INotificationsRepository,
            EfNotificationsRepository>();

        // ── Identity Trámites 2.0 (#9415–#9419) ───────────────────────
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityAccountRepository,
            NpgsqlIdentityAccountRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityRbacRepository,
            NpgsqlIdentityRbacRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.ITramitesPermissionVerifier,
            Flit.Modules.Identity.Application.TramitesPermissionVerifier>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityOnboardingRepository,
            NpgsqlIdentityOnboardingRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IGlobalAuthSettingsReader,
            NpgsqlGlobalAuthSettingsReader>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityPasswordResetRepository,
            NpgsqlIdentityPasswordResetRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityAdminRepository,
            NpgsqlIdentityAdminRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentityProfileRepository,
            NpgsqlIdentityProfileRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IIdentitySupportRepository,
            NpgsqlIdentitySupportRepository>();

        // ── IdentityVerification / IDSecure (Feature #9469) ───────────
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationActivationRepository,
            EfIdentityVerificationActivationRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IIdSecureCatalogLookup,
            EfIdSecureCatalogLookup>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationInvitationRepository,
            EfVerificationInvitationRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationSessionRepository,
            EfVerificationSessionRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationSessionStepRepository,
            EfVerificationSessionStepRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationEvidenceRepository,
            EfVerificationEvidenceRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IIdSecureFileRepository,
            EfIdSecureFileRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationOcrResultRepository,
            EfVerificationOcrResultRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationAiVerdictRepository,
            EfVerificationAiVerdictRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IVerificationManualOverrideRepository,
            EfVerificationManualOverrideRepository>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.IIdSecureReviewReadRepository,
            EfIdSecureReviewReadRepository>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureDocumentOcrProvider,
            Flit.Modules.IdentityVerification.Adapters.MockIdSecureDocumentOcrProvider>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureBiometricEvaluator,
            Flit.Modules.IdentityVerification.Adapters.MockIdSecureBiometricEvaluator>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecureCrossMatchEvaluator,
            Flit.Modules.IdentityVerification.Adapters.DocumentNumberCrossMatchEvaluator>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationProvider,
            Flit.Modules.IdentityVerification.Adapters.MockIdentityVerificationProvider>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IProcedureIdentityAdvanceNotifier,
            Flit.Modules.IdentityVerification.Adapters.CaptureProcedureIdentityAdvanceNotifier>();
        services.AddSingleton<
            Flit.Modules.IdentityVerification.Adapters.InMemoryProcedureIdentityValidationRepository>();
        services.AddSingleton<
            Flit.Modules.IdentityVerification.Ports.IProcedureIdentityValidationRepository>(
            sp => sp.GetRequiredService<
                Flit.Modules.IdentityVerification.Adapters.InMemoryProcedureIdentityValidationRepository>());
        services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationAuditRepository>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdentityVerificationAuditRepository>(
            sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryIdentityVerificationAuditRepository>());
        services.AddIdSecureBlobStorage(configuration, environment);
        services.AddSingleton<Flit.Modules.IdentityVerification.Adapters.InMemoryDataAccessLogRepository>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IDataAccessLogRepository>(
            sp => sp.GetRequiredService<Flit.Modules.IdentityVerification.Adapters.InMemoryDataAccessLogRepository>());
        services.AddSingleton<Flit.Modules.IdentityVerification.Application.IInvitationTokenHasher,
            Flit.Modules.IdentityVerification.Application.Sha256InvitationTokenHasher>();
        services.AddSingleton<Flit.Modules.IdentityVerification.Application.IInvitationTokenGenerator,
            Flit.Modules.IdentityVerification.Application.SecureInvitationTokenGenerator>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        var smtpHost = configuration[$"{SmtpOptions.SectionName}:Host"];
        var hasRealSmtp = !string.IsNullOrWhiteSpace(smtpHost)
                          && smtpHost is not ("localhost" or "127.0.0.1" or "::1");
        var useDevCapture = configuration.GetValue("IdSecure:Smtp:UseDevCapture", defaultValue: false);

        if (environment.IsDevelopment() && useDevCapture && !hasRealSmtp)
        {
            services.AddScoped<Flit.Modules.IdentityVerification.Ports.IInvitationEmailSender,
                DevLoggingInvitationEmailSender>();
        }
        else
        {
            services.AddScoped<SmtpEmailService>();
            services.AddScoped<Flit.Modules.IdentityVerification.Ports.IInvitationEmailSender,
                SmtpInvitationEmailSender>();
        }
        services.AddSingleton<Flit.Modules.IdentityVerification.Ports.IIdSecurePublicUrlProvider,
            ConfigurationIdSecurePublicUrlProvider>();
        services.AddScoped<Flit.Modules.IdentityVerification.Ports.ITramiteCreatedConsumer,
            Flit.Modules.IdentityVerification.Adapters.TramiteCreatedConsumer>();

        // ── Identity LEGACY (eliminar en Fase 7 con HybridCognito) ────
        services.AddScoped<Flit.Modules.Identity.Ports.IUsuariosRepository, EfUsuariosRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.ICredentialsRepository, EfCredentialsRepository>();
        services.AddScoped<Flit.Modules.Identity.Ports.IRefreshTokenStore, EfRefreshTokenStore>();

        // ── Companies (#9444 / #9445) ─────────────────────────────────
        services.AddScoped<ICompaniesRepository, EfCompaniesRepository>();
        services.AddScoped<ICompanyTenantProvisioner, IdentityCompanyTenantProvisioner>();
        services.AddScoped<ICompaniesIndexRepository, NpgsqlCompaniesIndexRepository>();
        services.AddScoped<ICompanyModuleConfigsRepository, EfCompanyModuleConfigsRepository>();
        services.AddScoped<ITenantGovernanceRepository, NpgsqlTenantGovernanceRepository>();
        services.AddScoped<IVehicleOwnershipRulesRepository, EfVehicleOwnershipRulesRepository>();

        // ── Webhooks QX (#9459 INT-02) ─────────────────────────────────
        services.AddScoped<IWebhookEventRepository, EfWebhookEventRepository>();

        // ── OT QX Integrations (#9455 OT-02) ───────────────────────────
        services.AddScoped<IOtQxIntegrationRepository, EfOtQxIntegrationRepository>();

        // ── OT Rules (#9456 OT-03) ──────────────────────────────────────
        services.AddScoped<IOtRuleRepository, EfOtRuleRepository>();
        services.AddScoped<IOtConsolidatedOrderRepository, EfOtConsolidatedOrderRepository>();

        // ── RUNT contingencia (#9447) ───────────────────────────────────
        services.AddSingleton<IRuntProviderCircuitBreaker, InMemoryRuntProviderCircuitBreaker>();
        services.AddScoped<IRuntVehicleQueryProvider, MockRuntVehicleQueryProvider>();
        services.AddScoped<IRuntVehicleQueryProvider, MockVerifikVehicleQueryProvider>();
        services.AddScoped<IRuntVehicleQueryProvider, MockIntempoVehicleQueryProvider>();
        services.AddScoped<IRuntSyncLogRepository, EfRuntSyncLogRepository>();
        services.AddScoped<IProcedureQueryResultsRepository, EfProcedureQueryResultsRepository>();

        // ── Procedures Config — RGL-02 (#9438) + RGL-03 (#9439) ────────
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureRulesRepository,
            NpgsqlProcedureRulesRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureRulesCatalogRepository,
            NpgsqlProcedureRulesCatalogRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IEndpointCallLogRepository,
            NpgsqlEndpointCallLogRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IRuleExecutionLogRepository,
            NpgsqlRuleExecutionLogRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IEndpointCatalogRepository,
            NpgsqlEndpointCatalogRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IRuleEndpointInvoker,
            Flit.Modules.ProceduresConfig.Application.CatalogRuleEndpointInvoker>();

        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProceduresConfigReadRepository,
            NpgsqlProceduresConfigReadRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProceduresConfigAdminRepository,
            NpgsqlProceduresConfigAdminRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IDocumentTypesReadRepository,
            NpgsqlDocumentTypesReadRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureFilingAuditPort,
            Flit.Modules.ProceduresConfig.Adapters.NoOpProcedureFilingAuditPort>();

        // ── Document templates — DOC-01 #9442 ───────────────────────────
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IDocumentTemplateRepository,
            NpgsqlDocumentTemplateRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureDocumentRepository,
            NpgsqlProcedureDocumentRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedurePdfFileStore,
            NpgsqlProcedurePdfFileStore>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IOtConsolidatedDocOrderRepository,
            NpgsqlOtConsolidatedDocOrderRepository>();

        // ── Procedures Runtime — TRA-02 #9434 ─────────────────────────
        services.AddScoped<Flit.Modules.Procedures.Domain.IProcedureInstanceRepository,
            NpgsqlProcedureInstanceRepository>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureInstanceExistsPort,
            Flit.Modules.Procedures.Adapters.ProcedureInstanceExistsAdapter>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IProcedureInstanceTrafficAgencyPort,
            Flit.Modules.Procedures.Adapters.ProcedureInstanceTrafficAgencyAdapter>();
        services.AddScoped<Flit.Modules.ProceduresConfig.Ports.IApprovedIdentityEvidencePort,
            Flit.Modules.IdentityVerification.Adapters.ApprovedIdentityEvidenceAdapter>();
        services.AddScoped<Flit.Modules.Procedures.Ports.IProcedureQueryResultRepository,
            NpgsqlProcedureQueryResultRepository>();
        services.AddScoped<Flit.Modules.Procedures.Ports.IProcedureFieldValueRepository,
            NpgsqlProcedureFieldValueRepository>();
        services.AddScoped<Flit.Modules.Procedures.Ports.IProcedureStateHistoryRepository,
            NpgsqlProcedureStateHistoryRepository>();
        services.AddScoped<Flit.Modules.Procedures.Ports.IProcedureActorRepository,
            NpgsqlProcedureActorRepository>();
        services.AddScoped<Flit.Modules.Procedures.Ports.IProcedureVehicleRepository,
            NpgsqlProcedureVehicleRepository>();

        // ── Integrations — INT-01 #9431 ────────────────────────────────
        services.AddScoped<Flit.Modules.Integrations.Ports.IExternalQueryCallLogRepository,
            NpgsqlExternalQueryCallLogRepository>();
        services.AddScoped<Flit.Modules.Integrations.Ports.IRuntSyncLogRepository,
            NpgsqlRuntSyncLogRepository>();

        return services;
    }
}
