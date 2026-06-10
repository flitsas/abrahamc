using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Repositorio en memoria unificado IDS-02/IDS-03 (tests y DEV sin Postgres).
/// </summary>
public sealed class InMemoryIdentityVerificationStore
    : IIdentityVerificationActivationRepository,
      IVerificationInvitationRepository,
      IVerificationSessionRepository,
      IVerificationSessionStepRepository,
      IVerificationEvidenceRepository,
      IIdSecureFileRepository,
      IVerificationOcrResultRepository,
      IVerificationAiVerdictRepository,
      IProcedureIdentityValidationRepository,
      IVerificationManualOverrideRepository,
      IIdSecureReviewReadRepository
{
    private readonly ConcurrentDictionary<(Guid TenantId, string Key), VerificationDomainEvent> _events = new();
    private readonly ConcurrentDictionary<Guid, VerificationInvitation> _invitations = new();
    private readonly ConcurrentDictionary<Guid, ProcedureTypeVerificationConfig> _configs = new();
    private readonly ConcurrentDictionary<Guid, VerificationEmailTemplate> _templates = new();
    private readonly ConcurrentDictionary<Guid, VerificationSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, VerificationSessionStep> _steps = new();
    private readonly ConcurrentDictionary<Guid, VerificationEvidence> _evidences = new();
    private readonly ConcurrentDictionary<Guid, IdSecureStoredFile> _files = new();
    private readonly ConcurrentDictionary<Guid, VerificationOcrResult> _ocrResults = new();
    private readonly ConcurrentDictionary<Guid, VerificationAiVerdict> _aiVerdicts = new();
    private readonly ConcurrentDictionary<Guid, ProcedureIdentityValidation> _identityValidations = new();
    private readonly ConcurrentDictionary<Guid, VerificationManualOverride> _manualOverrides = new();

    public void SeedConfig(ProcedureTypeVerificationConfig config) =>
        _configs[config.Id] = config;

    public void SeedTemplate(VerificationEmailTemplate template) =>
        _templates[template.Id] = template;

    public void SeedInvitation(VerificationInvitation invitation) =>
        _invitations[invitation.Id] = invitation;

    public void SeedDomainEvent(VerificationDomainEvent domainEvent) =>
        _events[(domainEvent.TenantId, domainEvent.IdempotencyKey)] = domainEvent;

    // ── IIdentityVerificationActivationRepository ─────────────────────

    public Task<bool> DomainEventExistsAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct) =>
        Task.FromResult(_events.ContainsKey((tenantId, idempotencyKey)));

    public Task<IReadOnlyList<ProcedureTypeVerificationConfig>> GetActiveConfigsAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct)
    {
        var list = _configs.Values
            .Where(c => c.TenantId == tenantId
                        && c.ProcedureTypeId == procedureTypeId
                        && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProcedureTypeVerificationConfig>>(list);
    }

    public Task<IReadOnlyList<VerificationInvitation>> GetInvitationsByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct)
    {
        var list = _invitations.Values
            .Where(i => i.TenantId == tenantId && i.ProcedureInstanceId == procedureInstanceId)
            .ToList();
        return Task.FromResult<IReadOnlyList<VerificationInvitation>>(list);
    }

    public Task RegisterDomainEventAsync(VerificationDomainEvent domainEvent, CancellationToken ct)
    {
        if (!_events.TryAdd((domainEvent.TenantId, domainEvent.IdempotencyKey), domainEvent))
            throw new Application.DuplicateDomainEventException();
        return Task.CompletedTask;
    }

    public Task AddInvitationsAsync(IReadOnlyList<VerificationInvitation> invitations, CancellationToken ct)
    {
        foreach (var invitation in invitations)
            _invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }

    // ── IVerificationInvitationRepository ─────────────────────────────

    public Task<VerificationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        var found = _invitations.Values.FirstOrDefault(i => i.TokenHash == tokenHash);
        return Task.FromResult(found);
    }

    Task<VerificationInvitation?> IVerificationInvitationRepository.GetByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct)
    {
        if (_invitations.TryGetValue(invitationId, out var inv) && inv.TenantId == tenantId)
            return Task.FromResult<VerificationInvitation?>(inv);
        return Task.FromResult<VerificationInvitation?>(null);
    }

    public Task<VerificationEmailTemplate?> FindEmailTemplateAsync(
        Guid tenantId,
        string templateKey,
        Guid? procedureTypeId,
        CancellationToken ct)
    {
        var found = _templates.Values.FirstOrDefault(t =>
            t.IsActive
            && t.TemplateKey == templateKey
            && (t.TenantId == null || t.TenantId == tenantId)
            && (procedureTypeId is null || t.ProcedureTypeId == procedureTypeId || t.ProcedureTypeId is null));
        return Task.FromResult(found);
    }

    public Task UpdateAsync(VerificationInvitation invitation, CancellationToken ct)
    {
        _invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public void SeedSession(VerificationSession session) =>
        _sessions[session.Id] = session;

    public void SeedSteps(IReadOnlyList<VerificationSessionStep> steps)
    {
        foreach (var step in steps)
            _steps[step.Id] = step;
    }

    // ── IVerificationSessionRepository ────────────────────────────────

    Task<VerificationSession?> IVerificationSessionRepository.GetByIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        if (_sessions.TryGetValue(verificationSessionId, out var session)
            && session.TenantId == tenantId)
            return Task.FromResult<VerificationSession?>(session);
        return Task.FromResult<VerificationSession?>(null);
    }

    public Task<VerificationSession?> GetByIdForPublicFlowAsync(
        Guid verificationSessionId,
        CancellationToken ct)
    {
        _sessions.TryGetValue(verificationSessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<VerificationSession?> FindByInvitationIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct)
    {
        var found = _sessions.Values.FirstOrDefault(
            s => s.TenantId == tenantId && s.VerificationInvitationId == invitationId);
        return Task.FromResult(found);
    }

    public Task AddAsync(VerificationSession session, CancellationToken ct)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VerificationSession session, CancellationToken ct)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<VerificationSession?> FindReusablePassedSessionAsync(
        Guid tenantId,
        string documentTypeCode,
        string documentNumber,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        var hit = _sessions.Values
            .Where(s =>
                s.TenantId == tenantId
                && string.Equals(s.DocumentTypeCode, documentTypeCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.SubjectDocumentNumber, documentNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Status, SessionStatus.Passed, StringComparison.OrdinalIgnoreCase)
                && s.ExpiresAt > asOf)
            .OrderByDescending(s => s.PerformedAt)
            .FirstOrDefault();

        return Task.FromResult(hit);
    }

    public async Task SaveAsync(VerificationSession session, CancellationToken ct = default)
    {
        if (_sessions.ContainsKey(session.Id))
            await UpdateAsync(session, ct);
        else
            await AddAsync(session, ct);
    }

    // ── IVerificationSessionStepRepository ────────────────────────────

    public Task<IReadOnlyList<VerificationSessionStep>> GetBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var list = _steps.Values
            .Where(s => s.TenantId == tenantId && s.VerificationSessionId == verificationSessionId)
            .OrderBy(s => s.StepNumber)
            .ToList();
        return Task.FromResult<IReadOnlyList<VerificationSessionStep>>(list);
    }

    public Task AddRangeAsync(IReadOnlyList<VerificationSessionStep> steps, CancellationToken ct)
    {
        foreach (var step in steps)
            _steps[step.Id] = step;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VerificationSessionStep sessionStep, CancellationToken ct)
    {
        _steps[sessionStep.Id] = sessionStep;
        return Task.CompletedTask;
    }

    // ── IVerificationEvidenceRepository ───────────────────────────────

    public Task AddAsync(VerificationEvidence evidence, CancellationToken ct)
    {
        _evidences[evidence.Id] = evidence;
        return Task.CompletedTask;
    }

    public Task<VerificationEvidence?> FindSignatureBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _evidences.Values.FirstOrDefault(e =>
            e.TenantId == tenantId
            && e.VerificationSessionId == verificationSessionId
            && e.EvidenceType == EvidenceTypes.SignatureCanvas);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<VerificationEvidence>> ListExpiredAsync(
        DateTimeOffset capturedBefore,
        CancellationToken ct)
    {
        var list = _evidences.Values
            .Where(e => !e.IsPurged && e.CapturedAt < capturedBefore)
            .ToList();
        return Task.FromResult<IReadOnlyList<VerificationEvidence>>(list);
    }

    public Task<IReadOnlyList<VerificationEvidence>> ListBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var list = _evidences.Values
            .Where(e => e.TenantId == tenantId && e.VerificationSessionId == verificationSessionId)
            .ToList();
        return Task.FromResult<IReadOnlyList<VerificationEvidence>>(list);
    }

    public Task UpdateAsync(VerificationEvidence evidence, CancellationToken ct)
    {
        _evidences[evidence.Id] = evidence;
        return Task.CompletedTask;
    }

    // ── IIdSecureFileRepository ─────────────────────────────────────

    public Task AddAsync(IdSecureStoredFile file, CancellationToken ct)
    {
        _files[file.Id] = file;
        return Task.CompletedTask;
    }

    Task<IdSecureStoredFile?> IIdSecureFileRepository.GetByIdAsync(
        Guid tenantId,
        Guid fileId,
        CancellationToken ct)
    {
        if (_files.TryGetValue(fileId, out var file) && file.TenantId == tenantId)
            return Task.FromResult<IdSecureStoredFile?>(file);
        return Task.FromResult<IdSecureStoredFile?>(null);
    }

    public Task UpdateAsync(IdSecureStoredFile file, CancellationToken ct)
    {
        _files[file.Id] = file;
        return Task.CompletedTask;
    }

    Task IIdSecureFileRepository.UpdateAsync(IdSecureStoredFile file, CancellationToken ct) =>
        UpdateAsync(file, ct);

    // ── IVerificationOcrResultRepository ────────────────────────────

    public Task AddAsync(VerificationOcrResult result, CancellationToken ct)
    {
        _ocrResults[result.Id] = result;
        return Task.CompletedTask;
    }

    public Task<VerificationOcrResult?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _ocrResults.Values.FirstOrDefault(
            r => r.TenantId == tenantId && r.VerificationSessionId == verificationSessionId);
        return Task.FromResult(found);
    }

    public Task UpdateAsync(VerificationOcrResult result, CancellationToken ct)
    {
        _ocrResults[result.Id] = result;
        return Task.CompletedTask;
    }

    // ── IVerificationAiVerdictRepository ──────────────────────────────

    Task IVerificationAiVerdictRepository.AddAsync(VerificationAiVerdict verdict, CancellationToken ct)
    {
        _aiVerdicts[verdict.Id] = verdict;
        return Task.CompletedTask;
    }

    Task<VerificationAiVerdict?> IVerificationAiVerdictRepository.FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _aiVerdicts.Values.FirstOrDefault(
            v => v.TenantId == tenantId && v.VerificationSessionId == verificationSessionId);
        return Task.FromResult(found);
    }

    Task IVerificationAiVerdictRepository.UpdateAsync(VerificationAiVerdict verdict, CancellationToken ct)
    {
        _aiVerdicts[verdict.Id] = verdict;
        return Task.CompletedTask;
    }

    // ── IProcedureIdentityValidationRepository ────────────────────────

    Task IProcedureIdentityValidationRepository.AddAsync(
        ProcedureIdentityValidation validation,
        CancellationToken ct)
    {
        _identityValidations[validation.Id] = validation;
        return Task.CompletedTask;
    }

    Task IProcedureIdentityValidationRepository.UpdateAsync(
        ProcedureIdentityValidation validation,
        CancellationToken ct)
    {
        _identityValidations[validation.Id] = validation;
        return Task.CompletedTask;
    }

    Task<ProcedureIdentityValidation?> IProcedureIdentityValidationRepository.FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _identityValidations.Values.FirstOrDefault(
            v => v.TenantId == tenantId && v.VerificationSessionId == verificationSessionId);
        return Task.FromResult(found);
    }

    Task<IReadOnlyList<ProcedureIdentityValidation>> IProcedureIdentityValidationRepository.GetByProcedureInstanceIdAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct)
    {
        var list = _identityValidations.Values
            .Where(v => v.TenantId == tenantId && v.ProcedureInstanceId == procedureInstanceId)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProcedureIdentityValidation>>(list);
    }

    Task<ProcedureIdentityValidation?> IProcedureIdentityValidationRepository.FindByInstanceAndActorAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid? actorId,
        CancellationToken ct)
    {
        var hit = _identityValidations.Values.FirstOrDefault(l =>
            l.TenantId == tenantId
            && l.ProcedureInstanceId == procedureInstanceId
            && l.ActorId == actorId);
        return Task.FromResult(hit);
    }

    Task IProcedureIdentityValidationRepository.SaveAsync(
        ProcedureIdentityValidation link,
        CancellationToken ct)
    {
        _identityValidations[link.Id] = link;
        return Task.CompletedTask;
    }

    // ── IVerificationManualOverrideRepository ─────────────────────────

    Task IVerificationManualOverrideRepository.AddAsync(
        VerificationManualOverride manualOverride,
        CancellationToken ct)
    {
        _manualOverrides[manualOverride.Id] = manualOverride;
        return Task.CompletedTask;
    }

    Task<VerificationManualOverride?> IVerificationManualOverrideRepository.FindLatestBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _manualOverrides.Values
            .Where(o => o.TenantId == tenantId && o.VerificationSessionId == verificationSessionId)
            .OrderByDescending(o => o.OverriddenAt)
            .FirstOrDefault();
        return Task.FromResult(found);
    }

    // ── IIdSecureReviewReadRepository ─────────────────────────────────

    Task<IReadOnlyList<VerificationSession>> IIdSecureReviewReadRepository.ListSessionsByTenantAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var list = _sessions.Values.Where(s => s.TenantId == tenantId).ToList();
        return Task.FromResult<IReadOnlyList<VerificationSession>>(list);
    }

    Task<VerificationInvitation?> IIdSecureReviewReadRepository.FindInvitationByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct)
    {
        if (_invitations.TryGetValue(invitationId, out var invitation) && invitation.TenantId == tenantId)
            return Task.FromResult<VerificationInvitation?>(invitation);
        return Task.FromResult<VerificationInvitation?>(null);
    }

    Task<VerificationDomainEvent?> IIdSecureReviewReadRepository.FindDomainEventByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct)
    {
        var found = _events.Values.FirstOrDefault(
            e => e.TenantId == tenantId && e.ProcedureInstanceId == procedureInstanceId);
        return Task.FromResult(found);
    }
}
