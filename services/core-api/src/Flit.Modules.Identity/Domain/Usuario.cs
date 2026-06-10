using Flit.SharedKernel;

namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Aggregate root del modulo Identity (ADR-0006).
/// Encapsula credenciales locales + cuentas externas vinculadas + rol RBAC.
/// </summary>
public sealed class Usuario
{
    public Guid Id { get; }
    public Email Email { get; private set; }
    public bool EmailVerificado { get; private set; }
    public Documento Documento { get; }
    public string Nombres { get; private set; }
    public string Apellidos { get; private set; }
    public DateOnly? FechaNacimiento { get; private set; }
    public string? Telefono { get; private set; }
    public Rol Rol { get; private set; }
    public Guid? OrganismoId { get; private set; }
    public bool Activo { get; private set; }
    public bool Bloqueado { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? UltimoLogin { get; private set; }
    public HabeasDataConsent Consent { get; private set; }
    public DateTimeOffset CreadoEn { get; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>
    /// Constructor sin parametros requerido por EF Core para hidratacion desde DB.
    /// No usar en codigo de aplicacion — usar la factory Crear().
    /// </summary>
#pragma warning disable CS8618 // EF Core hidrata propiedades desde DB.
    private Usuario()
    {
        Email = null!;
        Documento = null!;
        Nombres = string.Empty;
        Apellidos = string.Empty;
        Consent = new HabeasDataConsent(false, DateTimeOffset.MinValue, string.Empty);
    }
#pragma warning restore CS8618

    private Usuario(
        Guid id,
        Email email,
        Documento documento,
        string nombres,
        string apellidos,
        Rol rol,
        HabeasDataConsent consent,
        DateTimeOffset ahora)
    {
        Id = id;
        Email = email;
        EmailVerificado = false;
        Documento = documento;
        Nombres = nombres;
        Apellidos = apellidos;
        Rol = rol;
        Activo = true;
        Bloqueado = false;
        IntentosFallidos = 0;
        Consent = consent;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Factory de creacion. Exige consentimiento Habeas Data (Ley 1581).</summary>
    public static Result<Usuario, IdentityError> Crear(
        Email email,
        Documento documento,
        string? nombres,
        string? apellidos,
        Rol rol,
        bool consentimientoOtorgado,
        string politicaVersion,
        IClock clock)
    {
        if (!consentimientoOtorgado)
            return Result<Usuario, IdentityError>.Failure(
                new IdentityError.ConsentimientoRequerido());

        if (string.IsNullOrWhiteSpace(nombres) || nombres.Trim().Length < 2)
            return Result<Usuario, IdentityError>.Failure(
                new IdentityError.NombreInvalido("nombres minimo 2 caracteres"));

        if (string.IsNullOrWhiteSpace(apellidos) || apellidos.Trim().Length < 2)
            return Result<Usuario, IdentityError>.Failure(
                new IdentityError.NombreInvalido("apellidos minimo 2 caracteres"));

        var ahora = clock.UtcNow;
        var consent = new HabeasDataConsent(
            ConsentimientoOtorgado: true,
            FechaConsentimiento: ahora,
            PoliticaVersion: politicaVersion);

        var usuario = new Usuario(
            id: Guid.CreateVersion7(),
            email: email,
            documento: documento,
            nombres: nombres.Trim(),
            apellidos: apellidos.Trim(),
            rol: rol,
            consent: consent,
            ahora: ahora);

        return Result<Usuario, IdentityError>.Success(usuario);
    }

    /// <summary>Registra intento de login fallido. Bloquea por 15min al 5to fallo.</summary>
    public void RegistrarLoginFallido(IClock clock)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= 5)
        {
            Bloqueado = true;
            BloqueadoHasta = clock.UtcNow.AddMinutes(15);
        }
        ActualizadoEn = clock.UtcNow;
    }

    /// <summary>Registra login exitoso: limpia contadores.</summary>
    public void RegistrarLoginExitoso(IClock clock)
    {
        IntentosFallidos = 0;
        Bloqueado = false;
        BloqueadoHasta = null;
        UltimoLogin = clock.UtcNow;
        ActualizadoEn = clock.UtcNow;
    }

    public Result<Unit, IdentityError> ValidarPuedeLogin(IClock clock)
    {
        if (!Activo)
            return Result<Unit, IdentityError>.Failure(new IdentityError.CuentaInactiva());

        if (Bloqueado && BloqueadoHasta.HasValue && clock.UtcNow < BloqueadoHasta.Value)
            return Result<Unit, IdentityError>.Failure(
                new IdentityError.CuentaBloqueada(BloqueadoHasta.Value));

        // Si paso el tiempo de bloqueo, desbloquear automatico.
        if (Bloqueado && BloqueadoHasta.HasValue && clock.UtcNow >= BloqueadoHasta.Value)
        {
            Bloqueado = false;
            BloqueadoHasta = null;
            IntentosFallidos = 0;
        }

        return Result<Unit, IdentityError>.Success(Unit.Value);
    }

    public void Desactivar(IClock clock)
    {
        Activo = false;
        ActualizadoEn = clock.UtcNow;
    }

    public void AsignarRol(Rol rol, Guid? organismoId, IClock clock)
    {
        Rol = rol;
        OrganismoId = organismoId;
        ActualizadoEn = clock.UtcNow;
    }
}

/// <summary>Unit type para Result<Unit, TError> (equivalente a void exitoso).</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
