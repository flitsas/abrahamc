using Flit.Modules.IdentityVerification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

internal sealed class ProcedureTypeVerificationConfigConfiguration
    : IEntityTypeConfiguration<ProcedureTypeVerificationConfig>
{
    public void Configure(EntityTypeBuilder<ProcedureTypeVerificationConfig> builder)
    {
        builder.ToTable("procedure_type_verification_configs", "identity_verification");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(c => c.ProcedureTypeId).HasColumnName("procedure_type_id").HasColumnType("uuid").IsRequired();
        builder.Property(c => c.ParticipantRole).HasColumnName("participant_role").HasMaxLength(100).IsRequired();
        builder.Property(c => c.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(c => c.EmailTemplateKey).HasColumnName("email_template_key").HasMaxLength(100).IsRequired();
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasQueryFilter(c => EF.Property<DateTimeOffset?>(c, "DeletedAt") == null);

        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
    }
}

internal sealed class VerificationInvitationConfiguration
    : IEntityTypeConfiguration<VerificationInvitation>
{
    public void Configure(EntityTypeBuilder<VerificationInvitation> builder)
    {
        builder.ToTable("verification_invitations", "identity_verification");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.ProcedureInstanceId).HasColumnName("procedure_instance_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.ProcedureActorId).HasColumnName("procedure_actor_id").HasColumnType("uuid");
        builder.Property(i => i.ParticipantRole).HasColumnName("participant_role").HasMaxLength(100).IsRequired();
        builder.Property(i => i.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(320).IsRequired();
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(i => i.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(i => i.LastSentAt).HasColumnName("last_sent_at");
        builder.Property(i => i.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();

        builder.HasQueryFilter(i => EF.Property<DateTimeOffset?>(i, "DeletedAt") == null);

        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
        builder.Property<int>("ResentCount").HasColumnName("resent_count").HasDefaultValue(0);
    }
}

internal sealed class VerificationDomainEventConfiguration
    : IEntityTypeConfiguration<VerificationDomainEvent>
{
    public void Configure(EntityTypeBuilder<VerificationDomainEvent> builder)
    {
        builder.ToTable("verification_domain_events", "identity_verification");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(80).IsRequired();
        builder.Property(e => e.ProcedureInstanceId).HasColumnName("procedure_instance_id").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_verification_domain_events_idempotency");
    }
}

internal sealed class VerificationEmailTemplateConfiguration
    : IEntityTypeConfiguration<VerificationEmailTemplate>
{
    public void Configure(EntityTypeBuilder<VerificationEmailTemplate> builder)
    {
        builder.ToTable("verification_email_templates", "identity_verification");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(t => t.TemplateKey).HasColumnName("template_key").HasMaxLength(100).IsRequired();
        builder.Property(t => t.ProcedureTypeId).HasColumnName("procedure_type_id").HasColumnType("uuid");
        builder.Property(t => t.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
        builder.Property(t => t.HtmlBody).HasColumnName("html_body").IsRequired();
        builder.Property(t => t.Locale).HasColumnName("locale").HasMaxLength(10).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasQueryFilter(t => EF.Property<DateTimeOffset?>(t, "DeletedAt") == null);
        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
    }
}

internal sealed class VerificationSessionConfiguration
    : IEntityTypeConfiguration<VerificationSession>
{
    public void Configure(EntityTypeBuilder<VerificationSession> builder)
    {
        builder.ToTable("verification_sessions", "identity_verification");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.ProcedureInstanceId).HasColumnName("procedure_instance_id").HasColumnType("uuid");
        builder.Property(s => s.VerificationInvitationId).HasColumnName("verification_invitation_id").HasColumnType("uuid");
        builder.Property(s => s.ParticipantRole).HasColumnName("participant_role").HasMaxLength(100);
        builder.Property(s => s.CurrentStep).HasColumnName("current_step").HasDefaultValue((short)0);
        builder.Property(s => s.SubjectDocumentTypeId).HasColumnName("subject_document_type_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.SubjectDocumentNumber).HasColumnName("subject_document_number").HasMaxLength(50).IsRequired();
        builder.Property(s => s.Provider).HasColumnName("provider").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();

        builder.HasQueryFilter(s => EF.Property<DateTimeOffset?>(s, "DeletedAt") == null);
        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class VerificationSessionStepConfiguration
    : IEntityTypeConfiguration<VerificationSessionStep>
{
    public void Configure(EntityTypeBuilder<VerificationSessionStep> builder)
    {
        builder.ToTable("verification_session_steps", "identity_verification");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.VerificationSessionId).HasColumnName("verification_session_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<VerificationSession>()
            .WithMany()
            .HasForeignKey(s => s.VerificationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(s => s.StepNumber).HasColumnName("step_number").IsRequired();
        builder.Property(s => s.StepCode).HasColumnName("step_code").HasMaxLength(50).IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class VerificationOcrResultConfiguration
    : IEntityTypeConfiguration<VerificationOcrResult>
{
    public void Configure(EntityTypeBuilder<VerificationOcrResult> builder)
    {
        builder.ToTable("verification_ocr_results", "identity_verification");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.VerificationSessionId).HasColumnName("verification_session_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ExtractedFieldsJson).HasColumnName("extracted_fields").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ConfidenceScoresJson).HasColumnName("confidence_scores").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.RawProviderResponseJson).HasColumnName("raw_provider_response").HasColumnType("jsonb");
        builder.Property(r => r.ProcessedAt).HasColumnName("processed_at").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class VerificationAiVerdictConfiguration
    : IEntityTypeConfiguration<VerificationAiVerdict>
{
    public void Configure(EntityTypeBuilder<VerificationAiVerdict> builder)
    {
        builder.ToTable("verification_ai_verdicts", "identity_verification");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(v => v.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(v => v.VerificationSessionId).HasColumnName("verification_session_id").HasColumnType("uuid").IsRequired();
        builder.Property(v => v.Verdict).HasColumnName("verdict").HasMaxLength(20).IsRequired();
        builder.Property(v => v.BiometricScore).HasColumnName("biometric_score").HasColumnType("numeric(5,4)");
        builder.Property(v => v.LivenessPassed).HasColumnName("liveness_passed");
        builder.Property(v => v.CrossMatchPassed).HasColumnName("cross_match_passed");
        builder.Property(v => v.FailureReasonsJson).HasColumnName("failure_reasons").HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.DictamenJson).HasColumnName("dictamen").HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.DecidedAt).HasColumnName("decided_at").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(v => v.RowVersion).HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class VerificationManualOverrideConfiguration
    : IEntityTypeConfiguration<VerificationManualOverride>
{
    public void Configure(EntityTypeBuilder<VerificationManualOverride> builder)
    {
        builder.ToTable("verification_manual_overrides", "identity_verification");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.VerificationSessionId).HasColumnName("verification_session_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.PreviousVerdict).HasColumnName("previous_verdict").IsRequired();
        builder.Property(o => o.NewVerdict).HasColumnName("new_verdict").HasMaxLength(20).IsRequired();
        builder.Property(o => o.Reason).HasColumnName("reason").IsRequired();
        builder.Property(o => o.OverriddenBy).HasColumnName("overridden_by").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.OverriddenAt).HasColumnName("overridden_at").IsRequired();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.RowVersion).HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class VerificationEvidenceConfiguration
    : IEntityTypeConfiguration<VerificationEvidence>
{
    public void Configure(EntityTypeBuilder<VerificationEvidence> builder)
    {
        builder.ToTable("verification_evidences", "identity_verification");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.VerificationSessionId).HasColumnName("verification_session_id").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.EvidenceType).HasColumnName("evidence_type").HasMaxLength(30).IsRequired();
        builder.Property(e => e.FileId).HasColumnName("file_id").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.CapturedAt).HasColumnName("captured_at").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();

        builder.HasQueryFilter(e => EF.Property<DateTimeOffset?>(e, "DeletedAt") == null);
        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
        builder.Property<Guid?>("DeletedBy").HasColumnName("deleted_by").HasColumnType("uuid");
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
    }
}

internal sealed class IdSecureStoredFileConfiguration
    : IEntityTypeConfiguration<IdSecureStoredFile>
{
    public void Configure(EntityTypeBuilder<IdSecureStoredFile> builder)
    {
        builder.ToTable("files", "files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(f => f.Bucket).HasColumnName("bucket").IsRequired();
        builder.Property(f => f.ObjectKey).HasColumnName("object_key").IsRequired();
        builder.Property(f => f.OriginalFilename).HasColumnName("original_filename");
        builder.Property(f => f.ContentType).HasColumnName("content_type").IsRequired();
        builder.Property(f => f.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Ignore(f => f.StorageEncryption);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();

        builder.HasQueryFilter(f => EF.Property<DateTimeOffset?>(f, "DeletedAt") == null);
        builder.Property<DateTimeOffset?>("DeletedAt").HasColumnName("deleted_at");
        builder.Property<Guid?>("DeletedBy").HasColumnName("deleted_by").HasColumnType("uuid");
        builder.Property<int>("RowVersion").HasColumnName("row_version").HasDefaultValue(1);
        builder.Property<string>("Metadata").HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property<string?>("Sha256").HasColumnName("sha256").HasMaxLength(64);
    }
}
