using anin.dataAccess;
using anin.scm.Services;
using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Ingreso por seleccion de perfil, para trabajar fuera de la red de la
    /// ANIN. Sustituye a la pantalla del SSO y NADA MAS: emite el mismo JWT que
    /// TokenController y una sesion con la misma forma, de modo que el resto del
    /// sistema no sabe por donde entro el usuario.
    ///
    /// POR QUE EXISTE
    /// Cada rutina de negocio empieza por sigcm.paResolverActor, que exige la
    /// terna usuario-rol-unidad vigente. Sin identidad no hay forma de recorrer
    /// un solo paso del flujo, y el SSO institucional no responde desde una
    /// maquina local. La alternativa —cablear un usuario fijo en el codigo—
    /// impide justamente lo que hay que probar: que el mismo expediente ofrezca
    /// acciones distintas al especialista, al jefe, a OA y a Abastecimiento.
    ///
    /// QUE NO ES
    /// No es autenticacion. No hay contrasenia que verificar porque el SIGCM no
    /// guarda contrasenias (V001): la identidad la certifica el SSO. Quien llega
    /// a estos endpoints elige con quien entrar.
    ///
    /// POR ESO ESTA APAGADO EN PRODUCCION. Con appSettings:acceso_local distinto
    /// de "true" los tres endpoints responden 404 y la unica puerta vuelve a ser
    /// api/token/tksistema. La bandera se comprueba en cada llamada, no al
    /// arrancar, para que apagarla no dependa de reiniciar el servicio.
    /// </summary>
    [AllowAnonymous]
    public class AccesoController : ControllerBase
    {
        private const string CONEXION = "cnx_sigcm";

        private static bool AccesoLocalHabilitado()
        {
            return string.Equals(UT_Configuracion.AppSettings("appSettings", "acceso_local"),
                                 "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ternas usuario-rol-unidad vigentes hoy. Es la lista que reemplaza a la
        /// pantalla del SSO. Una fila por terna y no por persona: la misma cuenta
        /// puede ejercer dos roles y son dos ingresos con acciones distintas.
        /// </summary>
        [HttpGet]
        public IActionResult listarPerfil(string ipInput)
        {
            if (!AccesoLocalHabilitado()) return NotFound();

            DaProceso _Daproceso = new DaProceso();
            string strPayload = _Daproceso.ejecutarProceso(CONEXION, "sigcm.paListarPerfilAcceso",
                string.IsNullOrWhiteSpace(ipInput) ? "{}" : ipInput);

            return Ok(JsonDocument.Parse(strPayload));
        }

        /// <summary>
        /// Abre sesion con la terna elegida.
        /// Entrada: { "Cuenta":"...", "CodigoRol":"...", "CodigoUnidad":"..." }
        ///
        /// La respuesta imita a api/token/tksistema hasta en el nombre de los
        /// campos: { "estado":"OK", "mensaje": { ...sesion..., "token":"..." } }.
        /// El frontend guarda esa sesion sin saber de donde vino.
        ///
        /// La rutina vuelve a validar la vigencia de la terna. Elegir de la lista
        /// no basta: entre listar y entrar pudo vencer la asignacion, y sobre
        /// todo, un cliente puede llamar aqui con una terna que nunca estuvo en
        /// ninguna lista.
        /// </summary>
        [HttpPost]
        public IActionResult iniciarSesion(string ipInput)
        {
            if (!AccesoLocalHabilitado()) return NotFound();

            DaProceso _Daproceso = new DaProceso();
            string strPayload = _Daproceso.ejecutarProceso(CONEXION, "sigcm.paObtenerSesion",
                string.IsNullOrWhiteSpace(ipInput) ? "{}" : ipInput);

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

                        return Ok(JsonDocument.Parse(
                            "{\"estado\":\"ERROR\",\"mensaje\":" + JsonSerializer.Serialize(strMensaje) + "}"));
                    }

                    // CreateToken firma la sesion y sustituye el marcador @token
                    // que trae el payload por el JWT resultante. Mismo mecanismo
                    // que usa el ingreso por SSO.
                    string strSesionConToken = TokenService.CreateToken(jeSesion.GetRawText());

                    return Ok(JsonDocument.Parse(
                        "{\"estado\":\"OK\",\"mensaje\":" + strSesionConToken + "}"));
                }
            }
            catch (JsonException)
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":\"ERROR\",\"mensaje\":\"La rutina de sesion devolvio una respuesta ilegible.\"}"));
            }
        }

        /// <summary>
        /// Cierre de sesion local. Devuelve la ruta de retorno con la misma forma
        /// que api/token/LoginOut devuelve la del SSO, para que el frontend cierre
        /// igual en los dos casos. La sesion es el JWT en el navegador: aqui no
        /// hay nada que invalidar del lado del servidor.
        /// </summary>
        [HttpGet]
        public IActionResult loginOut()
        {
            if (!AccesoLocalHabilitado()) return NotFound();

            string strRuta = UT_Configuracion.AppSettings("appSettings", "acceso_local_ruta_salida")
                             ?? "/acceso-local";

            return Ok(JsonDocument.Parse(
                "{\"estado\":\"OK\",\"mensaje\":" + JsonSerializer.Serialize(strRuta) + "}"));
        }
    }
}
