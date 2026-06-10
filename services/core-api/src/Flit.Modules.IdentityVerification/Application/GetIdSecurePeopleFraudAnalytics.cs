using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9489 — Analytics People &amp; Fraud: KPIs por procedure_type y tasa de rechazo.
/// </summary>
public static class GetIdSecurePeopleFraudAnalytics
{
    public sealed record Query(Guid TenantId);

    public sealed record ProcedureTypeKpi(
        Guid? ProcedureTypeId,
        int TotalSessions,
        int ApprovedCount,
        int RejectedCount,
        int PendingCount,
        decimal RejectionRatePercent);

    public sealed record Response(
        IReadOnlyList<ProcedureTypeKpi> ByProcedureType,
        ProcedureTypeKpi Overall);

    public abstract record AnalyticsError(string Code, string Message, int HttpStatus)
    {
        public sealed record Forbidden()
            : AnalyticsError("FORBIDDEN", "Permiso idsecure.analytics requerido", 403);
    }

    public static async Task<Result<Response, AnalyticsError>> HandleAsync(
        Query query,
        bool hasAnalyticsPermission,
        IIdSecureReviewReadRepository readRepo,
        IVerificationAiVerdictRepository verdictRepo,
        IVerificationManualOverrideRepository overrideRepo,
        CancellationToken ct = default)
    {
        if (!hasAnalyticsPermission)
            return Result<Response, AnalyticsError>.Failure(new AnalyticsError.Forbidden());

        var sessions = await readRepo.ListSessionsByTenantAsync(query.TenantId, ct);
        var buckets = new Dictionary<Guid, (int Approved, int Rejected, int Pending)>();

        foreach (var session in sessions)
        {
            if (session.VerificationInvitationId is null)
                continue;

            var invitation = await readRepo.FindInvitationByIdAsync(
                query.TenantId, session.VerificationInvitationId.Value, ct);
            if (invitation is null)
                continue;

            Guid? procedureTypeId = null;
            if (session.ProcedureInstanceId is Guid instanceId)
            {
                var domainEvent = await readRepo.FindDomainEventByInstanceAsync(
                    query.TenantId, instanceId, ct);
                procedureTypeId = ListIdSecureReviewPanel.ExtractProcedureTypeId(domainEvent?.PayloadJson);
            }

            var groupKey = procedureTypeId ?? Guid.Empty;

            var latestOverride = await overrideRepo.FindLatestBySessionIdAsync(
                query.TenantId, session.Id, ct);
            var aiVerdict = await verdictRepo.FindBySessionIdAsync(
                query.TenantId, session.Id, ct);

            var currentVerdict = ListIdSecureReviewPanel.ResolveCurrentVerdict(
                session, aiVerdict, latestOverride);
            var bucket = ListIdSecureReviewPanel.ClassifyBucket(currentVerdict, session);

            if (!buckets.TryGetValue(groupKey, out var counts))
                counts = (0, 0, 0);

            counts = bucket switch
            {
                ReviewPanelBuckets.Approved => (counts.Approved + 1, counts.Rejected, counts.Pending),
                ReviewPanelBuckets.Rejected => (counts.Approved, counts.Rejected + 1, counts.Pending),
                _ => (counts.Approved, counts.Rejected, counts.Pending + 1),
            };

            buckets[groupKey] = counts;
        }

        var byType = buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => BuildKpi(
                kv.Key == Guid.Empty ? null : kv.Key,
                kv.Value))
            .ToList();

        var overallCounts = (
            Approved: buckets.Values.Sum(v => v.Approved),
            Rejected: buckets.Values.Sum(v => v.Rejected),
            Pending: buckets.Values.Sum(v => v.Pending));

        return Result<Response, AnalyticsError>.Success(
            new Response(byType, BuildKpi(null, overallCounts)));
    }

    internal static ProcedureTypeKpi BuildKpi(
        Guid? procedureTypeId,
        (int Approved, int Rejected, int Pending) counts)
    {
        var completed = counts.Approved + counts.Rejected;
        var rejectionRate = completed > 0
            ? Math.Round((decimal)counts.Rejected / completed * 100m, 2)
            : 0m;

        return new ProcedureTypeKpi(
            procedureTypeId,
            counts.Approved + counts.Rejected + counts.Pending,
            counts.Approved,
            counts.Rejected,
            counts.Pending,
            rejectionRate);
    }
}
