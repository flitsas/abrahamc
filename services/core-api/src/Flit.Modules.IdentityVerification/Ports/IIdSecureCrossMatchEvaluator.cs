namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Cross-matching OCR ↔ datos de sesión (HU #9486).
/// </summary>
public interface IIdSecureCrossMatchEvaluator
{
    CrossMatchEvaluationResult Evaluate(
        string? ocrDocumentNumber,
        string sessionDocumentNumber);
}

public sealed record CrossMatchEvaluationResult(
    bool Passed,
    string? FailureCode,
    string? FailureMessage);
