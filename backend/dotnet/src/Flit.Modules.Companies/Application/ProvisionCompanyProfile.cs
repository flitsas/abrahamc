using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9444 — aprovisiona maestro de compañía, billetera de firmas y configs modulares para un tenant.
/// </summary>
public static class ProvisionCompanyProfile
{
    public sealed record Request(
        Guid TenantId,
        string Nit,
        string LegalName,
        string? CommercialName,
        string ModulesEnabledJson,
        Guid ActorUserId,
        IReadOnlyList<ModuleConfigSeed>? ModuleConfigs = null,
        int InitialSignatureBalance = 0,
        int SignatureLowThreshold = 0,
        bool SignatureAutoRecharge = false);

    public sealed record ModuleConfigSeed(string ModuleKey, string ConfigJson);

    public sealed record Response(Guid CompanyId, Guid SignatureWalletId);

    public static async Task<Result<Response, CompaniesError>> ExecuteAsync(
        ICompaniesRepository repo,
        Func<CancellationToken, Task<int>> saveChanges,
        Request request,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (await repo.ExistsForTenantAsync(request.TenantId, ct))
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.TenantAlreadyHasCompany,
                "El tenant ya tiene una compañía registrada (relación 1:1)."));
        }

        var company = Company.Create(
            request.TenantId,
            request.Nit,
            request.LegalName,
            request.CommercialName,
            request.ModulesEnabledJson,
            request.ActorUserId,
            now);

        var wallet = SignatureWallet.Create(
            request.TenantId,
            request.InitialSignatureBalance,
            request.SignatureLowThreshold,
            request.SignatureAutoRecharge,
            request.ActorUserId,
            now);

        await repo.AddAsync(company, ct);
        await repo.AddSignatureWalletAsync(wallet, ct);

        if (request.ModuleConfigs is not null)
        {
            foreach (var seed in request.ModuleConfigs)
            {
                var cfg = CompanyModuleConfig.Create(
                    request.TenantId,
                    seed.ModuleKey,
                    seed.ConfigJson,
                    request.ActorUserId,
                    now);
                await repo.AddModuleConfigAsync(cfg, ct);
            }
        }

        try
        {
            await saveChanges(ct);
        }
        catch (Exception ex) when (IsTenantFkViolation(ex))
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.TenantNotFound,
                "tenant_id no existe en identity.tenants."));
        }

        return Result<Response, CompaniesError>.Success(new Response(company.Id, wallet.Id));
    }

    private static bool IsTenantFkViolation(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("fk_companies_tenants", StringComparison.OrdinalIgnoreCase)
            || message.Contains("fk_signature_wallets_tenants", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("23503", StringComparison.Ordinal) && message.Contains("tenants", StringComparison.OrdinalIgnoreCase));
    }
}
