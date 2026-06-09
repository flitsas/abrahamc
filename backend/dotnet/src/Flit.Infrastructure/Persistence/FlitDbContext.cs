using Flit.Infrastructure.Persistence.Joins;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Identity.Domain;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Rbac.Domain;
using Flit.Modules.Users.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Persistence;

/// <summary>
/// DbContext unificado del Modular Monolith FLIT 2.0 (shell post-reset trámites).
///
/// Schemas activos:
///   identity       — users, password_reset_tokens, user_audit_log, sync_inconsistencies
///   rbac           — roles, permissions, menu_items, joins
///   notifications          — notification_delivery
///   identity_verification  — IDSecure (Feature #9469)
///   companies              — companies B2B (#9381 / HU #9444)
///
/// Identity LEGACY (Usuario, IdentityCredential, RefreshTokenEntry) hasta Fase 7.
/// </summary>
public sealed class FlitDbContext(DbContextOptions<FlitDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();
    public DbSet<SyncInconsistency> SyncInconsistencies => Set<SyncInconsistency>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<UserRoleEntity> UserRoleAssignments => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<RoleMenuItemEntity> RoleMenuItems => Set<RoleMenuItemEntity>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyModuleConfig> CompanyModuleConfigs => Set<CompanyModuleConfig>();
    public DbSet<SignatureWallet> SignatureWallets => Set<SignatureWallet>();
    public DbSet<SignatureWalletMovement> SignatureWalletMovements => Set<SignatureWalletMovement>();
    public DbSet<VehicleOwnershipRule> VehicleOwnershipRules => Set<VehicleOwnershipRule>();

    public DbSet<RuntSyncLogEntry> RuntSyncLogs => Set<RuntSyncLogEntry>();
    public DbSet<ProcedureQueryResultSnapshot> ProcedureQueryResults => Set<ProcedureQueryResultSnapshot>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<OtQxIntegration> OtQxIntegrations => Set<OtQxIntegration>();
    public DbSet<OtRule> OtRules => Set<OtRule>();
    public DbSet<OtConsolidatedDocOrder> OtConsolidatedDocOrders => Set<OtConsolidatedDocOrder>();
    public DbSet<OtConsolidatedDocOrderItem> OtConsolidatedDocOrderItems => Set<OtConsolidatedDocOrderItem>();

    public DbSet<ProcedureTypeVerificationConfig> ProcedureTypeVerificationConfigs =>
        Set<ProcedureTypeVerificationConfig>();
    public DbSet<VerificationInvitation> VerificationInvitations => Set<VerificationInvitation>();
    public DbSet<VerificationDomainEvent> VerificationDomainEvents => Set<VerificationDomainEvent>();
    public DbSet<VerificationEmailTemplate> VerificationEmailTemplates => Set<VerificationEmailTemplate>();
    public DbSet<VerificationSession> VerificationSessions => Set<VerificationSession>();
    public DbSet<VerificationSessionStep> VerificationSessionSteps => Set<VerificationSessionStep>();
    public DbSet<VerificationEvidence> VerificationEvidences => Set<VerificationEvidence>();
    public DbSet<IdSecureStoredFile> IdSecureStoredFiles => Set<IdSecureStoredFile>();
    public DbSet<VerificationOcrResult> VerificationOcrResults => Set<VerificationOcrResult>();
    public DbSet<VerificationAiVerdict> VerificationAiVerdicts => Set<VerificationAiVerdict>();
    public DbSet<VerificationManualOverride> VerificationManualOverrides => Set<VerificationManualOverride>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<IdentityCredential> Credenciales => Set<IdentityCredential>();
    public DbSet<RefreshTokenEntry> RefreshTokens => Set<RefreshTokenEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlitDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
