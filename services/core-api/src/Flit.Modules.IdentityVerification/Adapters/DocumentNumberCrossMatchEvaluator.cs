using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Cross-match documento OCR vs número en sesión (mock DEV — HU #9486).
/// </summary>
public sealed class DocumentNumberCrossMatchEvaluator : IIdSecureCrossMatchEvaluator
{
    public const string MismatchMarker = "DOC_MISMATCH";

    public CrossMatchEvaluationResult Evaluate(
        string? ocrDocumentNumber,
        string sessionDocumentNumber)
    {
        if (string.IsNullOrWhiteSpace(ocrDocumentNumber)
            || string.IsNullOrWhiteSpace(sessionDocumentNumber)
            || sessionDocumentNumber == "PENDING")
        {
            return new CrossMatchEvaluationResult(
                false,
                CrossMatchFailureCodes.CfI5DocumentMismatch,
                "Documento OCR no coincide con la sesión de verificación");
        }

        if (ocrDocumentNumber.Contains(MismatchMarker, StringComparison.OrdinalIgnoreCase))
        {
            return new CrossMatchEvaluationResult(
                false,
                CrossMatchFailureCodes.CfI5DocumentMismatch,
                "Cross-match fallido: documento no coincide con datos del participante");
        }

        var passed = string.Equals(
            ocrDocumentNumber.Trim(),
            sessionDocumentNumber.Trim(),
            StringComparison.Ordinal);

        return passed
            ? new CrossMatchEvaluationResult(true, null, null)
            : new CrossMatchEvaluationResult(
                false,
                CrossMatchFailureCodes.CfI5DocumentMismatch,
                "Cross-match fallido: documento no coincide con datos del participante");
    }
}
