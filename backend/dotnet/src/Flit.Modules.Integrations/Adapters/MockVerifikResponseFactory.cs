using System.Text.Json;

namespace Flit.Modules.Integrations.Adapters;

/// <summary>
/// Respuestas mock con forma Verifik (DEV) — sin PII real (#9431 / #9467).
/// Referencia: APIs de Consulta del RUNT - Verifik.md
/// Cubre: RUNT (vehículo), RUNT_CONDUCTOR, SIMIT, RUES, RNMC, RES/RESOLUCIONES, FASECOLDA.
/// </summary>
public static class MockVerifikResponseFactory
{
    private const string MockSignatureDateTime = "Jun 04, 2026 12:00 PM";
    private const string MockSignatureMessage = "Certified by Verifik.co [MOCK-DEV]";

    public static JsonElement Build(string connectorCode, JsonElement? requestPayload)
    {
        var plate = ReadField(requestPayload, "placa", "plate", "noPlaca") ?? "DEMO01";
        var docType = ReadField(requestPayload, "documentTypeCode", "documentType") ?? "CC";
        var docNumber = ReadField(requestPayload, "documentNumber", "nit", "numeroDocumento") ?? "0000000000";
        var vin = ReadField(requestPayload, "vin", "noVin") ?? "MOCKVIN00000000001";

        var normalized = connectorCode.ToUpperInvariant() switch
        {
            "RUNT" => BuildRuntVehicle(plate, docType, docNumber, vin),
            "RUNT_CONDUCTOR" or "RUNT_DRIVER" => BuildRuntConductor(docType, docNumber),
            "SIMIT" => BuildSimit(docType, docNumber, plate),
            "RUES" => BuildRues(docType, docNumber),
            "RNMC" => BuildRnmc(docType, docNumber),
            "RES" or "RESOLUCIONES" => BuildResoluciones(docType, docNumber),
            "FASECOLDA" => BuildFasecolda(plate, vin),
            _ => BuildGeneric(connectorCode, plate),
        };

        return JsonSerializer.SerializeToElement(normalized);
    }

    // ── RUNT ─────────────────────────────────────────────────────────────────

    private static object BuildRuntVehicle(string plate, string docType, string docNumber, string vin) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            plate = plate.ToUpperInvariant(),
            vin,
            garantiasFavorDe = new[]
            {
                new
                {
                    tipoDocumentoAcreedor = "NIT",
                    numeroDocumentoAcreedor = "MOCK-NIT-ACREEDOR",
                    acreedor = "ENTIDAD FINANCIERA MOCK S.A.",
                    fechaInscripcion = "01/01/2024",
                    confecamaras = "NO",
                    patrimonioAutonomo = (string?)null,
                },
            },
            garantiasMobiliarias = Array.Empty<object>(),
            informacionBlindaje = new
            {
                autorizacion = (string?)null,
                blindado = "NO",
                fechaBlindaje = (string?)null,
                fechaDesblindaje = (string?)null,
                nivelBlindaje = (string?)null,
            },
            informacionGeneral = new
            {
                cilindraje = "1500",
                claseVehiculo = "AUTOMOVIL",
                clasicoAntiguo = "NO",
                color = "BLANCO",
                estadoDelVehiculo = "ACTIVO",
                fechaMatricula = "01/01/2024",
                linea = "MODELO DEV",
                marca = "MOCK",
                modelo = "2024",
                noChasis = "MOCKCHASIS0000001",
                noLicenciaTransito = "MOCK-LT-00001",
                noMotor = "MOCKMOTOR0000001",
                noPlaca = plate.ToUpperInvariant(),
                noVin = vin,
                organismoTransito = "SECRETARÍA MOVILIDAD MOCK",
                pasajerosSentados = "5",
                prendas = "NO",
                repotenciado = "NO",
                tieneGravamenes = "NO",
                tipoCarroceria = "SEDAN",
                tipoCombustible = "GASOLINA",
                tipoServicio = "Particular",
                seguridadEstado = "NO",
                vehiculoEnsenanza = "NO",
            },
            limitacionPropiedad = Array.Empty<object>(),
            solicitudes = new[]
            {
                new
                {
                    noSolicitud = "MOCK-SOL-0001",
                    fechaSolicitud = "01/01/2024",
                    estado = "APROBADA",
                    tramitesRealizados = "TRAMITE MATRÍCULA INICIAL",
                    entidad = "ORGANISMO DE TRÁNSITO MOCK",
                },
            },
            soat = new[]
            {
                new
                {
                    origen = "NACIONAL",
                    tipoTarifa = "130",
                    noPoliza = "MOCK-POL-00001",
                    fechaExpedicion = "01/12/2025",
                    fechaVigencia = "01/01/2026",
                    fechaVencimiento = "31/12/2026",
                    entidadExpideSoat = "ASEGURADORA MOCK S.A.",
                    estado = "VIGENTE",
                    estadoSoat = "EMITIDA",
                },
            },
            tarjetaOperacion = new
            {
                empresaAfiliadora = (string?)null,
                estado = (string?)null,
                fechaExpedicion = (string?)null,
                modalidadTransporte = (string?)null,
                nroTarjetaOperacion = (string?)null,
            },
            tecnoMecanica = new[]
            {
                new
                {
                    fechaExpedicion = "01/01/2025",
                    fechaVencimiento = "01/01/2026",
                    cdaExpide = "C.D.A. MOCK S.A.S.",
                    estado = "APROBADA",
                    tipoRevision = "REVISION TECNICO-MECANICO",
                    vigente = "SI",
                    nroCertificado = "MOCK-RTM-00001",
                    numeroPlaca = plate.ToUpperInvariant(),
                    informacionConsistente = "SI",
                },
            },
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-runt",
    };

    // ── RUNT CONDUCTOR ───────────────────────────────────────────────────────

    private static object BuildRuntConductor(string docType, string docNumber) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            citizenStatus = "ACTIVA",
            driverStatus = "ACTIVO",
            fullName = "CONDUCTOR MOCK DEV",
            firstName = "CONDUCTOR",
            lastName = "MOCK DEV",
            inscriptionNumber = "MOCK-INS-00001",
            inscriptionDate = "01/01/2020",
            totalLicenses = "1",
            licenses = new[]
            {
                new
                {
                    category = "B1",
                    status = "ACTIVA",
                    expeditionDate = "01/01/2020",
                    dueDate = "01/01/2030",
                    licenceNumber = docNumber,
                    restrictions = "NINGUNA",
                    otExpide = "ORGANISMO DE TRÁNSITO MOCK",
                },
            },
            infractions = new
            {
                nroPazYSalvo = "MOCK-PYS-00001",
                tieneMultas = "NO",
            },
            aptitudeCertificates = Array.Empty<object>(),
            medicalCertificates = Array.Empty<object>(),
            requests = Array.Empty<object>(),
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-runt-conductor",
    };

    // ── SIMIT ────────────────────────────────────────────────────────────────

    private static object BuildSimit(string docType, string docNumber, string plate) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            placa = plate.ToUpperInvariant(),
            tieneMultas = "NO",
            nroPazYSalvo = "MOCK-PYS-SIMIT-001",
            totalComparendos = 0,
            multas = Array.Empty<object>(),
            pazYSalvo = true,
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-simit",
    };

    // ── RUES ─────────────────────────────────────────────────────────────────

    private static object BuildRues(string docType, string docNumber) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            fullNit = $"{docNumber}-1",
            businessName = "EMPRESA MOCK DEV S.A.S.",
            organizationType = "SOCIEDAD ó PERSONA JURIDICA PRINCIPAL ó ESAL",
            location = "BOGOTA, D.C. / BOGOTA",
            status = "ACTIVA",
            idRm = "MOCK-RM-00000001",
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-rues",
    };

    // ── RNMC ─────────────────────────────────────────────────────────────────

    private static object BuildRnmc(string docType, string docNumber) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            tieneMedidas = false,
            totalMedidas = 0,
            medidasCorrectivas = Array.Empty<object>(),
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-rnmc",
    };

    // ── RES / RESOLUCIONES ───────────────────────────────────────────────────

    private static object BuildResoluciones(string docType, string docNumber) => new
    {
        data = new
        {
            documentType = docType,
            documentNumber = docNumber,
            resoluciones = Array.Empty<object>(),
            count = 0,
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-res",
    };

    // ── FASECOLDA ────────────────────────────────────────────────────────────

    private static object BuildFasecolda(string plate, string vin) => new
    {
        data = new
        {
            placa = plate.ToUpperInvariant(),
            vin,
            codigoFasecolda = "MOCK-FASE-001",
            descripcion = "AUTOMOVIL MOCK DEV",
            marca = "MOCK",
            linea = "MODELO DEV",
            modelo = "2024",
            valorReferencia = 0,
            cilindraje = "1500",
            tipoCombustible = "GASOLINA",
        },
        signature = new { dateTime = MockSignatureDateTime, message = MockSignatureMessage },
        environment = "mock-dev",
        source = "verifik-shaped-fasecolda",
    };

    // ── GENERIC ──────────────────────────────────────────────────────────────

    private static object BuildGeneric(string connector, string plate) => new
    {
        connector,
        plate = plate.ToUpperInvariant(),
        environment = "mock-dev",
        snapshot = new { status = "ok" },
    };

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static string? ReadField(JsonElement? payload, params string[] keys)
    {
        if (payload is not { } root || root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var v = prop.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v;
                }
            }
        }

        return null;
    }
}
