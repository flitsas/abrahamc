namespace Flit.Modules.Integrations.Application;

/// <summary>Timeout o fallo transitorio — habilita reintento idempotente interno (AC2 #9431).</summary>
public sealed class TransientExternalQueryException(string message) : Exception(message);
