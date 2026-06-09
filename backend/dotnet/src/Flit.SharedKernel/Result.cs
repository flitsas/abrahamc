namespace Flit.SharedKernel;

/// <summary>
/// Result Pattern (ADR-0002 §8.1 — "no exceptions para flujo de negocio").
/// Una instancia es success XOR failure.
/// </summary>
public readonly record struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    public bool IsSuccess { get; }

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value);

    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Acceso directo al value. Lanza InvalidOperationException si IsSuccess=false.
    /// Recomendado para tests y casos donde el caller ya verifico IsSuccess.
    /// En codigo de produccion preferir Match() para forzar manejo de ambos lados.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is failure; use Match or check IsSuccess");

    /// <summary>
    /// Acceso directo al error. Lanza InvalidOperationException si IsSuccess=true.
    /// </summary>
    public TError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Result is success; use Match or check IsSuccess");
}
