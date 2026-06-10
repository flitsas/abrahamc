# APIs de Consulta del RUNT - Verifik

Este documento detalla las APIs de consulta proporcionadas por Verifik.co para el Registro Único Nacional de Tránsito (RUNT), Registro Único Empresarial y Social (RUES), Sistema Integrado de Información sobre Multas y Sanciones por Infracciones de Tránsito (SIMIT) y Registro Nacional de Medidas Correctivas (RNMC).

## 1. Consulta de Vehículo por Placa

**URL de la API:**
`https://api.verifik.co/v2/co/runt/vehicle-by-plate?documentType=NIT&documentNumber=890903938&plate=ESP841`

**JSON de la consulta por placa:**
```json
{
    "data": {
        "datosTecnicos": {
            "alto": null,
            "ancho": null,
            "capacidadCarga": null,
            "largo": null,
            "noEjes": null,
            "noLlantas": null,
            "pasajerosSentados": "2",
            "pasajerosTotal": null,
            "peso": null,
            "pesoBrutoVehicular": null,
            "rodaje": null
        },
        "documentNumber": "1037669356",
        "documentType": "CC",
        "garantiasFavorDe": [
            {
                "tipoDocumentoAcreedor": "NIT",
                "numeroDocumentoAcreedor": "891410137",
                "acreedor": "SUZUKI MOTOR DE COLOMBIA S.A.",
                "fechaInscripcion": "22/12/2023",
                "confecamaras": "NO",
                "patrimonioAutonomo": null
            }
        ],
        "garantiasMobiliarias": [],
        "informacionBlindaje": {
            "autorizacion": null,
            "blindado": "NO",
            "fechaBlindaje": null,
            "fechaDesblindaje": null,
            "fechaExpedicionCertificado": null,
            "fechaExpedicionCertificadoFormatoWS": null,
            "idDocumentoCertificadoBlindaje": null,
            "nivelBlindaje": null,
            "nivelBlindajeNumero": null,
            "numeroResolucion": null,
            "tipoBlindajeNombre": null
        },
        "informacionGeneral": {
            "capacidadCarga": null,
            "cilindraje": "249",
            "claseVehiculo": "MOTOCICLETA",
            "clasicoAntiguo": "NO",
            "clasificacion": "MOTO",
            "color": "AZUL MATE",
            "diasMatriculado": "881",
            "esRegrabadoChasis": "NO",
            "esRegrabadoMotor": "NO",
            "esRegrabadoSerie": "NO",
            "esRegrabadoVin": "NO",
            "estadoDelVehiculo": "ACTIVO",
            "fechaExpedLTImportacion": null,
            "fechaMatricula": "22/12/2023",
            "fechaVenciLTImportacion": null,
            "idTipoServicio": "1",
            "linea": "GIXXER 250",
            "marca": "SUZUKI",
            "modelo": "2024",
            "mostrarSolicitudes": "SI",
            "noChasis": "9FSED24LXRC106299",
            "noEjes": null,
            "noIdentificacion": null,
            "noLicenciaTransito": "10030769490",
            "noMotor": "EJA1-181744",
            "noPlaca": "XJI21G",
            "noSerie": null,
            "noVin": "9FSED24LXRC106299",
            "nombrePais": null,
            "numRegraChasis": null,
            "numRegraMotor": null,
            "numRegraSerie": null,
            "numRegraVin": null,
            "organismoTransito": "STRIA TTOyTTE MCPAL SABANETA",
            "pasajerosSentados": "2",
            "pasajerosTotal": null,
            "pesoBruto": null,
            "prendas": "SI",
            "puertas": null,
            "repotenciado": "NO",
            "seguridadEstado": "NO",
            "subpartida": null,
            "tarjetaServicio": "NO",
            "tieneGravamenes": "NO",
            "tieneLTImportacion": false,
            "tipoCarroceria": "SIN CARROCERIA",
            "tipoCombustible": "GASOLINA",
            "tipoMaquinaria": null,
            "tipoServicio": "Particular",
            "validacionDIAN": "No Exitoso",
            "vehiculoEnsenanza": "NO",
            "verValidaDIAN": false
        },
        "limitacionPropiedad": [],
        "normalizacionSaneamiento": [
            {
                "deficienciaMatriculaInicial": "NO",
                "vehiculoNormalizado": "NO DISPONIBLE",
                "fecha": null,
                "numeroActoAdministrativo": null,
                "descargaCertificado": null,
                "solicitudNormalizacion": null
            }
        ],
        "plate": "XJI21G",
        "polizasResponsabilidadCivil": [],
        "soat": [
            {
                "origen": "NACIONAL",
                "tipoTarifa": "130",
                "noPoliza": "94547996",
                "fechaExpedicion": "20/12/2025",
                "fechaExpediSoat": "20/12/2025",
                "fechaVigencia": "22/12/2025",
                "fechaVencimiento": "21/12/2026",
                "entidadExpideSoat": "COMPAÑIA MUNDIAL DE SEGUROS S A",
                "estado": "VIGENTE",
                "estadoSoat": "EMITIDA",
                "placa": null,
                "nombrePais": null
            },
            {
                "origen": "NACIONAL",
                "tipoTarifa": "130",
                "noPoliza": "90129389",
                "fechaExpedicion": "19/12/2024",
                "fechaExpediSoat": "19/12/2024",
                "fechaVigencia": "22/12/2024",
                "fechaVencimiento": "21/12/2025",
                "entidadExpideSoat": "COMPAÑIA MUNDIAL DE SEGUROS S A",
                "estado": "NO VIGENTE",
                "estadoSoat": "EMITIDA",
                "placa": null,
                "nombrePais": null
            },
            {
                "origen": "NACIONAL",
                "tipoTarifa": "130",
                "noPoliza": "1508005559022000",
                "fechaExpedicion": "21/12/2023",
                "fechaExpediSoat": "21/12/2023",
                "fechaVigencia": "22/12/2023",
                "fechaVencimiento": "21/12/2024",
                "entidadExpideSoat": "LA PREVISORA S.A.COMPAÑIA DE SEGUROS",
                "estado": "NO VIGENTE",
                "estadoSoat": "EMITIDA",
                "placa": null,
                "nombrePais": null
            }
        ],
        "solicitudes": [
            {
                "noSolicitud": "283631396",
                "fechaSolicitud": "22/12/2025",
                "estado": "APROBADA",
                "tramitesRealizados": "Tramite revision tecnico mecanica, ",
                "entidad": "C.D.A HANGARES S.A.S. PALACE"
            },
            {
                "noSolicitud": "225768778",
                "fechaSolicitud": "22/12/2023",
                "estado": "AUTORIZADA",
                "tramitesRealizados": "TRÁMITE MATRÍCULA INICIAL, TRÁMITE INSCRIPCIÓN ALERTA, ",
                "entidad": "STRIA TTOyTTE MCPAL SABANETA"
            }
        ],
        "tarjetaOperacion": {
            "empresaAfiliadora": null,
            "estado": null,
            "fechaExpedicion": null,
            "fechaFin": null,
            "fechaInicio": null,
            "modalidadTransporte": null,
            "nroTarjetaOperacion": null,
            "radioAccion": null,
            "servicio": null
        },
        "tecnoMecanica": [
            {
                "fechaExpedicion": "22/12/2025",
                "fechaVencimiento": "22/12/2026",
                "cdaExpide": "C.D.A HANGARES S.A.S. PALACE",
                "estado": "APROBADA",
                "tipoRevision": "REVISION TECNICO-MECANICO",
                "vigente": "SI",
                "nroCertificado": "186323985",
                "numeroPlaca": "XJI21G",
                "informacionConsistente": "SI",
                "url": "d6393390-b423-41ac-b4fc-d786944f8391"
            }
        ],
        "vin": "9FSED24LXRC106299"
    },
    "signature": {
        "dateTime": "May 21, 2026 10:50 PM",
        "message": "Certified by Verifik.co"
    },
    "id": "Q6GQH"
}
```

## 2. Consulta de Conductor

**URL de la API:**
`https://api.verifik.co/v2/co/runt/conductor?documentType=CC&documentNumber=1110533751`

**JSON del conductor:**
```json
{
    "data": {
        "ANSVpayments": [],
        "aptitudeCertificates": [
            {
                "categoria": "B1",
                "descripcionTramite": "Trámite expedición licencia conducción",
                "estadoDocumento": "UTILIZADO",
                "fechaExpedicion": "23/08/2023",
                "fechaSolicitud": "23/08/2023",
                "fechaVencimiento": null,
                "idPersona": "22700402",
                "nombreCea": "CERVIAL",
                "tipoCertificado": "CERTIFICADO CONDUCTOR"
            },
            {
                "categoria": "A2",
                "descripcionTramite": "Trámite expedición licencia conducción",
                "estadoDocumento": "UTILIZADO",
                "fechaExpedicion": "23/08/2023",
                "fechaSolicitud": "23/08/2023",
                "fechaVencimiento": null,
                "idPersona": "22700402",
                "nombreCea": "CERVIAL",
                "tipoCertificado": "CERTIFICADO CONDUCTOR"
            }
        ],
        "citizenStatus": "ACTIVA",
        "consultationDateTime": "2026-05-21T22:52:41.812Z",
        "documentNumber": "1037669356",
        "documentType": "CC",
        "driverStatus": "ACTIVO",
        "fullName": "WILLYN SMITH LONDOÑO CALLE",
        "identityValidationAttempts": {
            "estadoUsuario": "ACTIVO",
            "fechaDesbloqueo": null,
            "validaciones": [
                {
                    "descripcionValidacion": "Solicitud finalizada por no completar el proceso",
                    "estadoHomologacionValidacion": "NO FINALIZADA",
                    "estadoValidacion": "EXPIRADO",
                    "fechaValidacion": "03/12/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Solicitud en estado no utilizada y vencida",
                    "estadoHomologacionValidacion": "VENCIDA",
                    "estadoValidacion": "ANULADO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Solicitud utilizada en la radicación del trámite",
                    "estadoHomologacionValidacion": "UTILIZADA",
                    "estadoValidacion": "UTILIZADO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Se generó rechazó en el proceso",
                    "estadoHomologacionValidacion": "FALLIDA",
                    "estadoValidacion": "FALLIDO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                }
            ]
        },
        "identityValidationRequests": {
            "estadoUsuario": "ACTIVO",
            "fechaDesbloqueo": null,
            "validaciones": [
                {
                    "descripcionValidacion": "Solicitud finalizada por no completar el proceso",
                    "estadoHomologacionValidacion": "NO FINALIZADA",
                    "estadoValidacion": "EXPIRADO",
                    "fechaValidacion": "03/12/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Solicitud en estado no utilizada y vencida",
                    "estadoHomologacionValidacion": "VENCIDA",
                    "estadoValidacion": "ANULADO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Solicitud utilizada en la radicación del trámite",
                    "estadoHomologacionValidacion": "UTILIZADA",
                    "estadoValidacion": "UTILIZADO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                },
                {
                    "descripcionValidacion": "Se generó rechazó en el proceso",
                    "estadoHomologacionValidacion": "FALLIDA",
                    "estadoValidacion": "FALLIDO",
                    "fechaValidacion": "19/11/2024",
                    "placaModValidacion": null,
                    "placaValidacion": null
                }
            ]
        },
        "infractions": {
            "nroPazYSalvo": "877081957069",
            "tieneMultas": "NO"
        },
        "inscriptionDate": "25/03/2023",
        "inscriptionNumber": "22700402",
        "licenses": [
            {
                "authorityTransit": null,
                "category": "A2",
                "dueDate": "24/08/2033",
                "endDateSuspension": null,
                "examExpirationDate": null,
                "expeditionDate": "24/08/2023",
                "licenceNumber": "1037669356",
                "otExpide": "STRIA TTEyTTO MCPAL CALDAS/ANTIOQUIA",
                "resolutionNumber": null,
                "restrictions": "CONDUCIR CON LENTES",
                "startDateSuspension": null,
                "status": "ACTIVA",
                "substratum": "1037669356"
            },
            {
                "authorityTransit": null,
                "category": "B1",
                "dueDate": "24/08/2033",
                "endDateSuspension": null,
                "examExpirationDate": null,
                "expeditionDate": "24/08/2023",
                "licenceNumber": "1037669356",
                "otExpide": "STRIA TTEyTTO MCPAL CALDAS/ANTIOQUIA",
                "resolutionNumber": null,
                "restrictions": "CONDUCIR CON LENTES",
                "startDateSuspension": null,
                "status": "ACTIVA",
                "substratum": "1037669356"
            }
        ],
        "medicalCertificates": [],
        "requests": [
            {
                "descripcionTipoValidacion": null,
                "descripcionValidacion": null,
                "entidad": "INPS TTOYTTE LA DORADA",
                "estadoSolicitud": "APROBADA",
                "estadoTramite": "APROBADO",
                "fechaSolicitud": "19/11/2024",
                "identificador": "C 1037669356",
                "nombreTramite": "Trámite certificado de capacitación en normas de transito",
                "numeroSolicitud": "254899652",
                "registro": "RNC",
                "tramitesRealizados": "181"
            },
            {
                "descripcionTipoValidacion": null,
                "descripcionValidacion": null,
                "entidad": "STRIA TTEyTTO MCPAL CALDAS/ANTIOQUIA",
                "estadoSolicitud": "AUTORIZADA",
                "estadoTramite": "APROBADO",
                "fechaSolicitud": "24/08/2023",
                "identificador": "C 1037669356",
                "nombreTramite": "Trámite expedición licencia conducción",
                "numeroSolicitud": "216510978",
                "registro": "RNC",
                "tramitesRealizados": "29"
            },
            {
                "descripcionTipoValidacion": null,
                "descripcionValidacion": null,
                "entidad": "CERVIAL",
                "estadoSolicitud": "APROBADA",
                "estadoTramite": "APROBADO",
                "fechaSolicitud": "23/08/2023",
                "identificador": "C 1037669356",
                "nombreTramite": "TRÁMITE CERTIFICADO APTITUD EN CONDUCCIÓN",
                "numeroSolicitud": "216514893",
                "registro": "RNC",
                "tramitesRealizados": "35"
            },
            {
                "descripcionTipoValidacion": null,
                "descripcionValidacion": null,
                "entidad": "CERVIAL",
                "estadoSolicitud": "APROBADA",
                "estadoTramite": "APROBADO",
                "fechaSolicitud": "23/08/2023",
                "identificador": "C 1037669356",
                "nombreTramite": "TRÁMITE CERTIFICADO APTITUD EN CONDUCCIÓN",
                "numeroSolicitud": "216499958",
                "registro": "RNC",
                "tramitesRealizados": "35"
            },
            {
                "descripcionTipoValidacion": null,
                "descripcionValidacion": null,
                "entidad": "MEDICLINICO SAN FRANCISCO SAS",
                "estadoSolicitud": "APROBADA",
                "estadoTramite": "APROBADO",
                "fechaSolicitud": "19/08/2023",
                "identificador": "C 1037669356",
                "nombreTramite": "Tramite certificado aptitud fisica mental motriz",
                "numeroSolicitud": "216320811",
                "registro": "RNC",
                "tramitesRealizados": "38"
            }
        ],
        "sicovRequests": [],
        "totalLicenses": "2",
        "transitTaxes": {},
        "firstName": "WILLYN SMITH",
        "lastName": "LONDOÑO CALLE",
        "arrayName": [
            "WILLYN",
            "SMITH",
            "LONDOÑO",
            "CALLE"
        ]
    },
    "signature": {
        "dateTime": "May 21, 2026 10:52 PM",
        "message": "Certified by Verifik.co"
    },
    "id": "9O6WT"
}
```

## 3. Consulta RUES

**URL de la API:**
`https://api.verifik.co/v2/co/rues?documentNumber=830104901&documentType=NIT`

**JSON de la consulta del RUES:**
```json
{
    "data": {
        "businessName": "USADOS MAXIAUTOS SAS",
        "documentNumber": "830104901",
        "documentType": "NIT",
        "fullNit": "830104901-1",
        "idRm": "40001192116",
        "location": "BOGOTA, D.C. / BOGOTA",
        "organizationType": "SOCIEDAD ó PERSONA JURIDICA PRINCIPAL ó ESAL",
        "status": "ACTIVA"
    },
    "signature": {
        "dateTime": "May 21, 2026 10:55 PM",
        "message": "Certified by Verifik.co"
    },
    "id": "OUDMK"
}
```

## 4. Consulta SIMIT Resoluciones

**URL de la API:**
`https://api.verifik.co/v2/co/simit/resoluciones?documentType=NIT&documentNumber=800229063`

**JSON de la consulta de resoluciones:**
```json
{
    "data": {
        "resoluciones": [
            {
                "estadosResoluciones": "Cobro coactivo",
                "fechaComparendo": "20170323",
                "fechaResolucion": "20170906",
                "fotodeteccion": false,
                "numeroComparendo": "05001000000015117539",
                "resoluciones": "0000813150",
                "secretarias": "Medellin",
                "total": "867562"
            },
            {
                "estadosResoluciones": "Cobro coactivo",
                "fechaComparendo": "20130502",
                "fechaResolucion": "20131030",
                "fotodeteccion": false,
                "numeroComparendo": "05001000000004027158",
                "resoluciones": "0000033983",
                "secretarias": "Medellin",
                "total": "998449"
            },
            {
                "estadosResoluciones": "Cobro coactivo",
                "fechaComparendo": "20140130",
                "fechaResolucion": "20140627",
                "fotodeteccion": false,
                "numeroComparendo": "05001000000005556784",
                "resoluciones": "0000134356",
                "secretarias": "Medellin",
                "total": "990318"
            }
        ]
    },
    "signature": {
        "dateTime": "May 21, 2026 10:57 PM",
        "message": "Certified by Verifik.co"
    },
    "id": "79AQ2"
}
```

## 5. Consulta RNMC o Medidas Correctivas

**URL de la API:**
`https://api.verifik.co/v2/co/policia/rnmc?documentType=CC&documentNumber=1037669356&date=17/07/2017`
