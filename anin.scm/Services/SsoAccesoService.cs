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
    ///   4. Con una, se abre la sesion. Con varias, la elige el usuario.
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
        /// una sesion lleva UNA terna. Quien ejerce dos -hoy la coordinadora que
        /// atiende Desarrollo de Sistemas y Abastecimiento- tiene que decir con
        /// cual entra, igual que en /acceso-local.
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

            Sincronizar("INGRESO", strCuenta, strEquipo);

            return ResolverSesion(strCuenta, strEquipo);
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

            string? strSobre = _DaSso.ArmarSobreSincronizacion(
                strDisparador, strCuenta, strEquipo, "SIGCM-SSO");

            if (string.IsNullOrWhiteSpace(strSobre))
            {
                return string.Empty;
            }

            DaProceso _Daproceso = new DaProceso();
            return _Daproceso.ejecutarProceso(CONEXION, "sigcm.paSincronizarPadronSso", strSobre, 120);
        }

        /// <summary>
        /// Cuantas ternas tiene la cuenta y que hacer con eso.
        /// </summary>
        private static string ResolverSesion(string strCuenta, string? strEquipo)
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
            try
            {
                using (JsonDocument jdIdentidad = JsonDocument.Parse(strIdentidad))
                {
                    JsonElement jeRaiz = jdIdentidad.RootElement;

                    if (jeRaiz.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    foreach (string strCampo in new[] { "usuario", "Usuario", "dni", "Dni" })
                    {
                        if (jeRaiz.TryGetProperty(strCampo, out JsonElement jeValor)
                            && jeValor.ValueKind == JsonValueKind.String)
                        {
                            string? strValor = jeValor.GetString();

                            if (!string.IsNullOrWhiteSpace(strValor))
                            {
                                return strValor.Trim();
                            }
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

        private static string Sobre(string strEstado, string strMensaje)
        {
            return string.Concat("{\"estado\":\"", strEstado, "\",\"mensaje\":", strMensaje, "}");
        }
    }
}
