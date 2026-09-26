using anin.dataAccess;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace anin.scm.Services
{
    /// <summary>
    /// El ingreso por SSO, de punta a punta.
    ///
    /// LOS CUATRO PASOS
    ///   1. El servicio de token del SSO certifica la identidad (UT_Sso).
    ///   2. Se trae el padron completo de la base del SSO (DaProcesoSso) y se
    ///      reconcilia contra DBSIGCM (sigcm.paSincronizarPadronSso).
    ///   3. Se preguntan las ternas vigentes de esa cuenta.
    ///   4. Con una, se abre la sesion. Con varias, se respeta el perfil que
    ///      el usuario YA eligio en el portal SSO (cod_perfil / centro_costo
    ///      del token). Solo si eso no alcanza se muestra el selector del SGCM.
    ///
    /// POR QUE SE SINCRONIZA EN CADA INGRESO Y NO POR UN PROCESO NOCTURNO
    /// Porque el padron son veinte filas y la corrida cuesta menos que la propia
    /// validacion del token. Lo caro seria lo contrario: una lista de derivacion
    /// que ofrece al especialista que renuncio la semana pasada.
    ///
    /// POR QUE UN FALLO DE SINCRONIZACION NO IMPIDE ENTRAR
    /// Si la base del SSO no responde, quien ya esta registrado sigue trabajando
    /// con el padron de la ultima corrida. Dejar a toda la entidad fuera del
    /// sistema porque un servidor de lectura esta caido seria cambiar una
    /// molestia por una interrupcion. El unico que no podra entrar es quien
    /// nunca se sincronizo, y para ese el mensaje lo dice.
    /// </summary>
    public class SsoAccesoService
    {
        private const string CONEXION = "cnx_sigcm";

        /// <summary>
        /// Ventana del pase intermedio. Es corto porque no da acceso a nada: solo
        /// acredita, entre la eleccion de perfil y la apertura de sesion, que
        /// esta persona ya presento un token valido del SSO.
        /// </summary>
        private const double MINUTOS_PRE_ACCESO = 10.0;

        private const string CLAIM_PRE_ACCESO = "sigcm_pre_acceso";

        /// <summary>
        /// Ingreso completo. Devuelve el JSON que el controlador entrega tal cual:
        ///
        ///   { "estado":"OK",     "mensaje": { ...sesion..., "token":"..." } }
        ///   { "estado":"PERFIL", "mensaje": { "Cuenta":"...", "PreToken":"...",
        ///                                     "Perfiles":[ ... ] } }
        ///   { "estado":"ERROR",  "mensaje": "..." }
        ///
        /// El caso PERFIL existe porque el frontend consume detalle[0].perfil[0]:
        /// una sesion lleva UNA terna. Si el token del SSO ya trae el perfil
        /// elegido (cod_perfil), se entra con esa terna. Si no, y solo hay una
        /// terna de area usuaria, se entra con esa. El selector del SGCM queda
        /// solo cuando el SSO no alcanza a desambiguar.
        /// </summary>
        public static string Ingresar(string strToken, string? strEquipo)
        {
            string strIdentidad = anin.util.UT_Sso.ValidarAcceso(strToken);

            if (string.IsNullOrWhiteSpace(strIdentidad) || strIdentidad == "{}" || strIdentidad == "-1")
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "El SSO no reconocio el token. Vuelva a ingresar desde el portal."));
            }

            string? strCuenta = LeerCuenta(strIdentidad);

            if (string.IsNullOrWhiteSpace(strCuenta))
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "El SSO valido el token pero no devolvio la cuenta del usuario."));
            }

            try
            {
                Sincronizar("INGRESO", strCuenta, strEquipo);
            }
            catch (Exception)
            {
                // Sin padron fresco se entra con la ultima terna local. Caer
                // aqui no puede impedir el Anexo 3 de quien ya esta registrado.
            }

            return ResolverSesion(strCuenta, strEquipo, strIdentidad, strToken);
        }

        /// <summary>
        /// Ingreso del portal externo (SGCM-E / locador).
        ///
        /// El token lo certifica validartokenexterno (sistema S0078). No se
        /// sincroniza el padron institucional: el locador no vive ahi. La
        /// sesion se arma con la misma paObtenerSesion que el ingreso interno,
        /// a partir de la terna en sigcm.UsuarioRol (p. ej. PROVEEDOR + D0001).
        /// Sin eso, tksistemaexterno firmaba el sobre crudo del SSO —sin
        /// cod_dependencia— y paResolverActor reventaba en falta Actor.Unidad.
        /// </summary>
        public static string IngresarExterno(string strToken, string? strEquipo)
        {
            string strIdentidad = anin.util.UT_Sso.ValidarAccesoExterno(strToken);

            if (string.IsNullOrWhiteSpace(strIdentidad) || strIdentidad == "{}" || strIdentidad == "-1")
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "Usuario y/o Clave Incorrecta."));
            }

            string? strCuenta = LeerCuenta(strIdentidad);

            if (string.IsNullOrWhiteSpace(strCuenta))
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "El SSO valido el token externo pero no devolvio la cuenta del usuario."));
            }

            return ResolverSesion(strCuenta, strEquipo, strIdentidad, strToken);
        }

        /// <summary>
        /// Segundo tramo, cuando el usuario eligio con que perfil entra.
        /// El pase intermedio es lo que impide que este endpoint sea una puerta
        /// abierta: sin el, cualquiera podria pedir la sesion de cualquier cuenta
        /// con solo saber su nombre de usuario. La cuenta la manda el pase, no el
        /// cliente, asi que tampoco sirve elegir el perfil de otra persona.
        /// </summary>
        public static string IniciarSesionConPerfil(string strPreToken, string strPayload, string? strEquipo)
        {
            string? strCuenta = LeerCuentaPreAcceso(strPreToken);

            if (string.IsNullOrWhiteSpace(strCuenta))
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "El pase de acceso vencio o no es valido. Vuelva a ingresar desde el portal."));
            }

            string? strRol = null, strUnidad = null;

            try
            {
                using (JsonDocument jdEntrada = JsonDocument.Parse(
                           string.IsNullOrWhiteSpace(strPayload) ? "{}" : strPayload))
                {
                    if (jdEntrada.RootElement.TryGetProperty("CodigoRol", out JsonElement jeRol))
                        strRol = jeRol.GetString();

                    if (jdEntrada.RootElement.TryGetProperty("CodigoUnidad", out JsonElement jeUnidad))
                        strUnidad = jeUnidad.GetString();
                }
            }
            catch (JsonException)
            {
                return Sobre("ERROR", JsonSerializer.Serialize("La eleccion de perfil no es un JSON valido."));
            }

            if (string.IsNullOrWhiteSpace(strRol) || string.IsNullOrWhiteSpace(strUnidad))
            {
                return Sobre("ERROR", JsonSerializer.Serialize("Falta el rol o la unidad del perfil elegido."));
            }

            return AbrirSesion(strCuenta, strRol, strUnidad, strEquipo);
        }

        /// <summary>
        /// Refresco del padron ANTES de mandar un correo.
        ///
        /// POR QUE EXISTE
        /// El correo de una persona lo gobierna el SSO, pero las rutinas arman el
        /// destinatario leyendo sigcm.Usuario, que es una REPLICA. Hasta ahora esa
        /// replica solo se refrescaba al ingresar, y el token dura ocho horas: quien
        /// ya estaba dentro trabajaba toda la jornada contra la foto del padron del
        /// momento en que entro.
        ///
        /// Lo que eso costaba, con fecha: el 2026-09-09 a las 00:33 se cambio en el
        /// SSO el correo de una cuenta, y el aviso del Anexo 4 de la 01:03 se fue
        /// igual a la direccion anterior. Se "arreglo solo" a las 09:59, cuando
        /// alguien volvio a entrar y el padron se reconcilio. Un aviso que llega a
        /// la bandeja equivocada no se puede deshacer.
        ///
        /// POR QUE AQUI Y NO EN UN MIDDLEWARE
        /// Una notificacion es infrecuente; una peticion no. Colgar la sincronizacion
        /// de cada request ataria la latencia de todas las pantallas a la salud de
        /// una base ajena. Se paga el costo donde importa que el dato este fresco.
        ///
        /// NUNCA LANZA. Si el SSO no responde se sigue con la replica que haya: un
        /// correo con el destinatario de ayer es preferible a un expediente que no
        /// avanza. Es la misma regla que ya gobierna el ingreso.
        /// </summary>
        public static void RefrescarAntesDeNotificar(string? strCuenta, string? strEquipo)
        {
            try
            {
                Sincronizar("NOTIFICACION", strCuenta, strEquipo);
            }
            catch (Exception)
            {
                // Deliberado: ver el parrafo "NUNCA LANZA" de arriba.
            }
        }

        /// <summary>
        /// Sincronizacion a pedido, para la opcion de mantenimiento. Devuelve el
        /// resumen de la reconciliacion con sus altas, sus bajas y los descartes.
        /// </summary>
        public static string SincronizarPadron(string? strCuenta, string? strEquipo)
        {
            string strRespuesta = Sincronizar("MANTENIMIENTO", strCuenta, strEquipo);

            return string.IsNullOrWhiteSpace(strRespuesta)
                ? "{\"estado\":0,\"mensaje\":\"No fue posible leer el padron del SSO.\"}"
                : strRespuesta;
        }

        /* ------------------------------------------------------------------ */

        /// <summary>
        /// Trae el padron y lo entrega a la rutina de reconciliacion. Devuelve
        /// cadena vacia si la base del SSO no respondio: el llamador decide si eso
        /// es fatal (mantenimiento) o no (ingreso).
        /// </summary>
        private static string Sincronizar(string strDisparador, string? strCuenta, string? strEquipo)
        {
            DaProcesoSso _DaSso = new DaProcesoSso();

            string strPadron = _DaSso.EjecutarProceso(
                "SELECT login.fn_listar_login_usuario_perfil_sistema_sgcm(NULL)::text;")
                .RootElement.GetRawText();
            string strDependencia = _DaSso.EjecutarProceso(@"
                SELECT COALESCE(json_agg(d)::text, '[]')
                  FROM (
                        SELECT id_dependencia, id_padre, cod_dependencia,
                               siglas, descripcion, centro_costo
                          FROM login.tm_login_dependencia
                         WHERE COALESCE(activo, true)
                           AND centro_costo IS NOT NULL
                           AND btrim(centro_costo) <> ''
                         ORDER BY id_dependencia
                       ) AS d;").RootElement.GetRawText();

            if (string.IsNullOrWhiteSpace(strPadron) || strPadron.StartsWith("{\"estado\":0")
                || string.IsNullOrWhiteSpace(strDependencia) || strDependencia.StartsWith("{\"estado\":0"))
            {
                return string.Empty;
            }

            string strSobre = string.Concat(
                "{\"Disparador\":", JsonSerializer.Serialize(strDisparador),
                ",\"Cuenta\":", JsonSerializer.Serialize(strCuenta ?? string.Empty),
                ",\"Completo\":true",
                ",\"Equipo\":", JsonSerializer.Serialize(strEquipo ?? "sso"),
                ",\"Programa\":\"SIGCM-SSO\"",
                ",\"Dependencia\":", strDependencia,
                ",\"Padron\":", strPadron,
                "}");

            DaProceso _Daproceso = new DaProceso();
            return _Daproceso.ejecutarProceso(CONEXION, "sigcm.paSincronizarPadronSso", strSobre, 120);
        }

        /// <summary>
        /// Cuantas ternas tiene la cuenta y que hacer con eso.
        /// </summary>
        private static string ResolverSesion(
            string strCuenta, string? strEquipo, string? strIdentidad, string? strToken)
        {
            DaProceso _Daproceso = new DaProceso();

            string strPerfiles = _Daproceso.ejecutarProceso(CONEXION, "sigcm.paListarPerfilSso",
                "{\"Cuenta\":" + JsonSerializer.Serialize(strCuenta) + "}");

            try
            {
                using (JsonDocument jdPerfiles = JsonDocument.Parse(strPerfiles))
                {
                    JsonElement jeRaiz = jdPerfiles.RootElement;

                    bool bCorrecto = jeRaiz.TryGetProperty("estado", out JsonElement jeEstado)
                                     && jeEstado.ValueKind == JsonValueKind.Number
                                     && jeEstado.GetInt32() == 1;

                    if (!bCorrecto || !jeRaiz.TryGetProperty("Perfiles", out JsonElement jePerfiles)
                                   || jePerfiles.ValueKind != JsonValueKind.Array)
                    {
                        return Sobre("ERROR", JsonSerializer.Serialize(
                            "No fue posible obtener los perfiles de la cuenta " + strCuenta + "."));
                    }

                    int intCantidad = jePerfiles.GetArrayLength();

                    // Autenticado en el SSO pero sin ninguna terna vigente aqui.
                    // Casi siempre es un cod_perfil sin mapear en sigcm.PerfilSso
                    // o un centro de costo sin unidad: los dos casos quedan
                    // anotados en Descartes de la ultima sincronizacion.
                    if (intCantidad == 0)
                    {
                        return Sobre("ERROR", JsonSerializer.Serialize(
                            "Su cuenta no tiene ningun perfil vigente en el SIGCM. " +
                            "Comuniquese con el administrador del sistema."));
                    }

                    if (intCantidad == 1)
                    {
                        JsonElement jeUnico = jePerfiles[0];

                        return AbrirSesion(strCuenta,
                            jeUnico.GetProperty("CodigoRol").GetString() ?? string.Empty,
                            jeUnico.GetProperty("CodigoUnidad").GetString() ?? string.Empty,
                            strEquipo);
                    }

                    /* El portal SSO ya hizo elegir perfil. Si el token trae
                       cod_perfil (y centro_costo cuando el mismo PE cubre dos
                       unidades), se abre esa terna y no se vuelve a preguntar. */
                    if (TryAbrirPorPerfilSso(strCuenta, strEquipo, strIdentidad, strToken, jePerfiles,
                            out string strSesionSso))
                    {
                        return strSesionSso;
                    }

                    /* Misma regla que el jefe de OTI: si solo hay una terna de
                       area usuaria, se entra con esa y no se pregunta. Evelyn
                       es coordinadora AU de OTI y ademas coordinadora de
                       Abastecimiento/UDS; Gustavo es jefe AU y administrador.
                       El selector queda para quien tiene dos sombreros AU. */
                    JsonElement jeAreaUsuaria = default;
                    int intAreaUsuaria = 0;

                    foreach (JsonElement jePerfil in jePerfiles.EnumerateArray())
                    {
                        string strRol = jePerfil.GetProperty("CodigoRol").GetString() ?? string.Empty;

                        if (strRol.StartsWith("AREA_", StringComparison.Ordinal))
                        {
                            intAreaUsuaria++;
                            jeAreaUsuaria = jePerfil;
                        }
                    }

                    if (intAreaUsuaria == 1)
                    {
                        return AbrirSesion(strCuenta,
                            jeAreaUsuaria.GetProperty("CodigoRol").GetString() ?? string.Empty,
                            jeAreaUsuaria.GetProperty("CodigoUnidad").GetString() ?? string.Empty,
                            strEquipo);
                    }

                    return Sobre("PERFIL", string.Concat(
                        "{\"Cuenta\":", JsonSerializer.Serialize(strCuenta),
                        ",\"PreToken\":", JsonSerializer.Serialize(CrearPreToken(strCuenta)),
                        ",\"Perfiles\":", jePerfiles.GetRawText(), "}"));
                }
            }
            catch (JsonException)
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "La rutina de perfiles devolvio una respuesta ilegible."));
            }
        }

        /// <summary>
        /// Abre la sesion con la terna que el SSO ya selecciono. Devuelve false
        /// si el token no trae perfil, o si no hay una unica terna coincidente:
        /// en ese caso el llamador sigue con las reglas de respaldo.
        /// </summary>
        private static bool TryAbrirPorPerfilSso(
            string strCuenta,
            string? strEquipo,
            string? strIdentidad,
            string? strToken,
            JsonElement jePerfiles,
            out string strSesion)
        {
            strSesion = string.Empty;

            string? strCodPerfil = PrimeroNoVacio(
                LeerCampoIdentidad(strIdentidad,
                    "cod_perfil", "CodigoPerfil", "codigo_perfil", "CodPerfil"),
                LeerCampoJwt(strToken,
                    "cod_perfil", "CodigoPerfil", "codigo_perfil", "CodPerfil"));
            if (string.IsNullOrWhiteSpace(strCodPerfil))
            {
                return false;
            }

            /* El sobre del SSO trae la dependencia elegida como
               detalle[].dependencia[].cod_dependencia (D0001), no como
               centro_costo. Sin filtrar por eso, PE071 coincide con OTI y UDS. */
            string? strCodUnidad = PrimeroNoVacio(
                LeerCampoIdentidad(strIdentidad,
                    "cod_dependencia", "CodigoUnidad", "codigo_unidad", "CodDependencia"),
                LeerCampoJwt(strToken,
                    "cod_dependencia", "CodigoUnidad", "codigo_unidad", "CodDependencia"));

            string? strCentro = PrimeroNoVacio(
                LeerCampoIdentidad(strIdentidad,
                    "centro_costo", "CentroCosto", "centroCosto", "cod_centro_costo"),
                LeerCampoJwt(strToken,
                    "centro_costo", "CentroCosto", "centroCosto", "cod_centro_costo"));

            HashSet<string> hsCentros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(strCentro))
            {
                foreach (string strParte in strCentro.Split(new[] { ';', ',' },
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    hsCentros.Add(strParte);
                }
            }

            List<JsonElement> lstCoinciden = new List<JsonElement>();

            foreach (JsonElement jePerfil in jePerfiles.EnumerateArray())
            {
                if (!PerfilContieneCodigoSso(jePerfil, strCodPerfil))
                {
                    continue;
                }

                if (!CoincideUnidadOCentro(jePerfil, strCodUnidad, hsCentros))
                {
                    continue;
                }

                lstCoinciden.Add(jePerfil);
            }

            /* Si el CSV no alcanzo (SQL viejo o PE no listado en la fila),
               se traduce el PE a CodigoRol y se filtra por eso + unidad/centro. */
            if (lstCoinciden.Count == 0)
            {
                string? strCodigoRolTraducido = TraducirCodigoPerfilSso(strCodPerfil);

                if (!string.IsNullOrWhiteSpace(strCodigoRolTraducido))
                {
                    foreach (JsonElement jePerfil in jePerfiles.EnumerateArray())
                    {
                        string strRol = jePerfil.TryGetProperty("CodigoRol", out JsonElement jeRol)
                            ? (jeRol.GetString() ?? string.Empty)
                            : string.Empty;

                        if (!string.Equals(strRol, strCodigoRolTraducido, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!CoincideUnidadOCentro(jePerfil, strCodUnidad, hsCentros))
                        {
                            continue;
                        }

                        lstCoinciden.Add(jePerfil);
                    }
                }
            }

            // Mismo PE en dos unidades y el token no trajo dependencia/centro:
            // sin desambiguar no se inventa. El selector (o AREA_*) sigue.
            if (lstCoinciden.Count != 1)
            {
                return false;
            }

            JsonElement jeElegido = lstCoinciden[0];
            strSesion = AbrirSesion(strCuenta,
                jeElegido.GetProperty("CodigoRol").GetString() ?? string.Empty,
                jeElegido.GetProperty("CodigoUnidad").GetString() ?? string.Empty,
                strEquipo);
            return true;
        }

        /// <summary>
        /// Si el SSO indico unidad o centro, la terna debe coincidir. Si no
        /// indico ninguno, cualquier unidad del perfil sirve (queda el conteo).
        /// </summary>
        private static bool CoincideUnidadOCentro(
            JsonElement jePerfil, string? strCodUnidad, HashSet<string> hsCentros)
        {
            bool bHayFiltro = !string.IsNullOrWhiteSpace(strCodUnidad) || hsCentros.Count > 0;
            if (!bHayFiltro)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(strCodUnidad))
            {
                string strUnidad = jePerfil.TryGetProperty("CodigoUnidad", out JsonElement jeUnidad)
                    ? (jeUnidad.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.Equals(strUnidad, strCodUnidad, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (hsCentros.Count > 0)
            {
                string strCcPerfil = jePerfil.TryGetProperty("CentroCosto", out JsonElement jeCc)
                    ? (jeCc.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (!string.IsNullOrEmpty(strCcPerfil) && hsCentros.Contains(strCcPerfil))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PerfilContieneCodigoSso(JsonElement jePerfil, string strCodPerfil)
        {
            if (jePerfil.TryGetProperty("CodigosPerfilSso", out JsonElement jeCodigos)
                && jeCodigos.ValueKind == JsonValueKind.String)
            {
                string? strCsv = jeCodigos.GetString();
                if (!string.IsNullOrWhiteSpace(strCsv))
                {
                    foreach (string strParte in strCsv.Split(',',
                                 StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (string.Equals(strParte, strCodPerfil, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            // Respaldo si el SQL aun no desplego CodigosPerfilSso: el propio
            // CodigoRol a veces llega igual que el PE (casos raros / externos).
            if (jePerfil.TryGetProperty("CodigoRol", out JsonElement jeRol))
            {
                string? strRol = jeRol.GetString();
                if (string.Equals(strRol, strCodPerfil, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? PrimeroNoVacio(params string?[] arrValores)
        {
            foreach (string? strValor in arrValores)
            {
                if (!string.IsNullOrWhiteSpace(strValor))
                {
                    return strValor.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Claims del JWT del portal (xy) sin validar firma: solo lectura de
        /// payload. Si el token no es JWT, no aporta nada.
        /// </summary>
        private static string? LeerCampoJwt(string? strToken, params string[] arrCampos)
        {
            if (string.IsNullOrWhiteSpace(strToken) || arrCampos == null || arrCampos.Length == 0)
            {
                return null;
            }

            try
            {
                JwtSecurityTokenHandler jsthManejador = new JwtSecurityTokenHandler();
                if (!jsthManejador.CanReadToken(strToken))
                {
                    return null;
                }

                JwtSecurityToken jwtToken = jsthManejador.ReadJwtToken(strToken);

                foreach (string strCampo in arrCampos)
                {
                    Claim? clClaim = jwtToken.Claims.FirstOrDefault(c =>
                        string.Equals(c.Type, strCampo, StringComparison.OrdinalIgnoreCase));

                    if (clClaim != null && !string.IsNullOrWhiteSpace(clClaim.Value))
                    {
                        return clClaim.Value.Trim();
                    }
                }

                // Payload como JSON por si el claim esta anidado.
                if (jwtToken.Payload != null && jwtToken.Payload.Count > 0)
                {
                    string strPayload = JsonSerializer.Serialize(jwtToken.Payload);
                    return LeerCampoIdentidad(strPayload, arrCampos);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? TraducirCodigoPerfilSso(string strCodPerfil)
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                string strRespuesta = _Daproceso.ejecutarProceso(CONEXION, "sigcm.paTraducirPerfilSso",
                    "{\"CodigoPerfilSso\":" + JsonSerializer.Serialize(strCodPerfil) + "}");

                using (JsonDocument jd = JsonDocument.Parse(strRespuesta))
                {
                    JsonElement jeRaiz = jd.RootElement;
                    bool bOk = jeRaiz.TryGetProperty("estado", out JsonElement jeEstado)
                               && jeEstado.ValueKind == JsonValueKind.Number
                               && jeEstado.GetInt32() == 1;

                    if (!bOk || !jeRaiz.TryGetProperty("CodigoRol", out JsonElement jeRol))
                    {
                        return null;
                    }

                    string? strRol = jeRol.GetString();
                    return string.IsNullOrWhiteSpace(strRol) ? null : strRol.Trim();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Arma la sesion con la terna elegida. Es la MISMA rutina que usa el
        /// ingreso local: misma forma de payload, mismas validaciones, mismo
        /// menu. Lo unico que cambia es Origen, que le dice al frontend por que
        /// puerta cerrar la sesion.
        /// </summary>
        private static string AbrirSesion(string strCuenta, string strRol, string strUnidad, string? strEquipo)
        {
            string strEntrada = string.Concat(
                "{\"Cuenta\":", JsonSerializer.Serialize(strCuenta),
                ",\"CodigoRol\":", JsonSerializer.Serialize(strRol),
                ",\"CodigoUnidad\":", JsonSerializer.Serialize(strUnidad),
                ",\"Origen\":\"SSO\"",
                ",\"Equipo\":", JsonSerializer.Serialize(strEquipo ?? "sso"),
                ",\"Programa\":\"SIGCM-SSO\"}");

            DaProceso _Daproceso = new DaProceso();
            string strPayload = _Daproceso.ejecutarProceso(CONEXION, "sigcm.paObtenerSesion", strEntrada);

            try
            {
                using (JsonDocument jdRespuesta = JsonDocument.Parse(strPayload))
                {
                    JsonElement jeRaiz = jdRespuesta.RootElement;

                    bool bCorrecto = jeRaiz.TryGetProperty("estado", out JsonElement jeEstado)
                                     && jeEstado.ValueKind == JsonValueKind.Number
                                     && jeEstado.GetInt32() == 1;

                    if (!bCorrecto || !jeRaiz.TryGetProperty("Sesion", out JsonElement jeSesion))
                    {
                        string strMensaje = jeRaiz.TryGetProperty("mensaje", out JsonElement jeMensaje)
                            ? jeMensaje.ToString()
                            : "No fue posible abrir la sesion.";

                        return Sobre("ERROR", JsonSerializer.Serialize(strMensaje));
                    }

                    // CreateToken firma la sesion y sustituye el marcador @token
                    // que trae el payload por el JWT resultante.
                    return Sobre("OK", TokenService.CreateToken(jeSesion.GetRawText()));
                }
            }
            catch (JsonException)
            {
                return Sobre("ERROR", JsonSerializer.Serialize(
                    "La rutina de sesion devolvio una respuesta ilegible."));
            }
        }

        /* ---- El pase intermedio ------------------------------------------ */

        private static string CrearPreToken(string strCuenta)
        {
            byte[] arrClave = Encoding.ASCII.GetBytes(Settings.Secret);
            JwtSecurityTokenHandler jsthManejador = new JwtSecurityTokenHandler();

            SecurityTokenDescriptor stdDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(CLAIM_PRE_ACCESO, strCuenta)
                }),
                Expires = DateTime.UtcNow.AddMinutes(MINUTOS_PRE_ACCESO),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(arrClave), SecurityAlgorithms.HmacSha256Signature)
            };

            return jsthManejador.WriteToken(jsthManejador.CreateToken(stdDescriptor));
        }

        private static string? LeerCuentaPreAcceso(string strPreToken)
        {
            if (string.IsNullOrWhiteSpace(strPreToken))
            {
                return null;
            }

            try
            {
                byte[] arrClave = Encoding.ASCII.GetBytes(Settings.Secret);
                JwtSecurityTokenHandler jsthManejador = new JwtSecurityTokenHandler();

                ClaimsPrincipal cpPrincipal = jsthManejador.ValidateToken(strPreToken,
                    new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(arrClave),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        // Sin holgura: la ventana ya es de diez minutos y este
                        // pase no debe sobrevivir a su vencimiento.
                        ClockSkew = TimeSpan.Zero
                    }, out SecurityToken stValidado);

                return cpPrincipal.FindFirst(CLAIM_PRE_ACCESO)?.Value;
            }
            catch (Exception)
            {
                // Vencido, alterado o firmado con otra clave: en los tres casos
                // la respuesta es la misma y no se detalla cual fue.
                return null;
            }
        }

        /* ---- Utilitarios -------------------------------------------------- */

        /// <summary>
        /// La cuenta dentro de la respuesta del servicio de token del SSO. Se
        /// busca por varios nombres porque el sobre no es identico en todos los
        /// sistemas de la ANIN, y el DNI sirve de ultimo recurso: en el padron
        /// del SGCM la cuenta ES el numero de documento.
        /// </summary>
        private static string? LeerCuenta(string strIdentidad)
        {
            return LeerCampoIdentidad(strIdentidad,
                "usuario", "Usuario", "dni", "Dni", "nro_documento");
        }

        /// <summary>
        /// Un campo del sobre de identidad del SSO. Prueba varios nombres y
        /// tambien la forma anidada detalle[0].perfil[0] / detalle[0] que
        /// usaba el ingreso anterior (antes de armar la sesion local).
        /// </summary>
        private static string? LeerCampoIdentidad(string? strIdentidad, params string[] arrCampos)
        {
            if (string.IsNullOrWhiteSpace(strIdentidad) || arrCampos == null || arrCampos.Length == 0)
            {
                return null;
            }

            try
            {
                using (JsonDocument jdIdentidad = JsonDocument.Parse(strIdentidad))
                {
                    JsonElement jeRaiz = jdIdentidad.RootElement;

                    if (jeRaiz.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    string? strDirecto = LeerCampoEnObjeto(jeRaiz, arrCampos);
                    if (!string.IsNullOrWhiteSpace(strDirecto))
                    {
                        return strDirecto;
                    }

                    // Forma historica del sobre SSO: detalle[].perfil[].cod_perfil
                    if (jeRaiz.TryGetProperty("detalle", out JsonElement jeDetalle)
                        || jeRaiz.TryGetProperty("Detalle", out jeDetalle))
                    {
                        JsonElement jePrimero = jeDetalle.ValueKind == JsonValueKind.Array
                            && jeDetalle.GetArrayLength() > 0
                            ? jeDetalle[0]
                            : jeDetalle;

                        if (jePrimero.ValueKind == JsonValueKind.Object)
                        {
                            string? strEnDetalle = LeerCampoEnObjeto(jePrimero, arrCampos);
                            if (!string.IsNullOrWhiteSpace(strEnDetalle))
                            {
                                return strEnDetalle;
                            }

                            if (jePrimero.TryGetProperty("perfil", out JsonElement jePerfil)
                                || jePrimero.TryGetProperty("Perfil", out jePerfil))
                            {
                                JsonElement jePerfil0 = jePerfil.ValueKind == JsonValueKind.Array
                                    && jePerfil.GetArrayLength() > 0
                                    ? jePerfil[0]
                                    : jePerfil;

                                if (jePerfil0.ValueKind == JsonValueKind.Object)
                                {
                                    string? strEnPerfil = LeerCampoEnObjeto(jePerfil0, arrCampos);
                                    if (!string.IsNullOrWhiteSpace(strEnPerfil))
                                    {
                                        return strEnPerfil;
                                    }
                                }
                            }

                            // detalle[].dependencia[].cod_dependencia / centro_costo
                            if (jePrimero.TryGetProperty("dependencia", out JsonElement jeDep)
                                || jePrimero.TryGetProperty("Dependencia", out jeDep))
                            {
                                JsonElement jeDep0 = jeDep.ValueKind == JsonValueKind.Array
                                    && jeDep.GetArrayLength() > 0
                                    ? jeDep[0]
                                    : jeDep;

                                if (jeDep0.ValueKind == JsonValueKind.Object)
                                {
                                    string? strEnDep = LeerCampoEnObjeto(jeDep0, arrCampos);
                                    if (!string.IsNullOrWhiteSpace(strEnDep))
                                    {
                                        return strEnDep;
                                    }
                                }
                            }
                        }
                    }

                    // Por si validartoken envuelve la identidad en data / usuario.
                    foreach (string strSobre in new[] { "data", "Data", "usuario", "Usuario", "mensaje", "Mensaje" })
                    {
                        if (!jeRaiz.TryGetProperty(strSobre, out JsonElement jeHijo)
                            || jeHijo.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        string? strEnHijo = LeerCampoEnObjeto(jeHijo, arrCampos);
                        if (!string.IsNullOrWhiteSpace(strEnHijo))
                        {
                            return strEnHijo;
                        }
                    }

                    return null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? LeerCampoEnObjeto(JsonElement jeObjeto, string[] arrCampos)
        {
            foreach (string strCampo in arrCampos)
            {
                if (!jeObjeto.TryGetProperty(strCampo, out JsonElement jeValor))
                {
                    continue;
                }

                string? strValor = jeValor.ValueKind switch
                {
                    JsonValueKind.String => jeValor.GetString(),
                    JsonValueKind.Number => jeValor.GetRawText(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(strValor))
                {
                    return strValor.Trim();
                }
            }

            return null;
        }

        private static string Sobre(string strEstado, string strMensaje)
        {
            return string.Concat("{\"estado\":\"", strEstado, "\",\"mensaje\":", strMensaje, "}");
        }
    }
}
