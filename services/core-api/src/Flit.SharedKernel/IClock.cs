namespace Flit.SharedKernel;

/// <summary>
/// Reloj inyectable. ADR-0002 §8.1: "DateTime.UtcNow (no DateTime.Now), inyectar IClock".
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Implementación de produccion. En tests se reemplaza con un fake.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
