using anin.dataAccess;
using anin.util;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Base de todos los controladores de esquema. Concentra lo unico que el
    /// backend aporta al flujo: identificar al actor y llamar a la rutina.
    ///
    /// POR QUE UNA BASE Y NO CODIGO REPETIDO EN CADA ENDPOINT
    /// El bloque Actor debe completarse DESDE LA SESION, sobrescribiendo lo que
    /// venga del navegador (SIGCM_SERVER/db/README.md). Si cada endpoint lo
    /// armara por su cuenta, bastaria con que uno se olvidara para que un
    /// cliente pudiera declararse jefe de otra unidad. Aqui es imposible
    /// olvidarlo: EjecutarConActor siempre reescribe el bloque.
    ///
    /// Un endpoint de este sistema no valida reglas de negocio, no arma SQL y no
    /// interpreta la respuesta. Recibe ipInput, lo pasa y devuelve lo que la
    /// rutina conteste. Todo lo demas vive en la base.
    /// </summary>
    public abstract class ControladorPuente : ControllerBase
    {
        protected const string CONEXION = "cnx_sigcm";

        /// <summary>
        /// Invoca la rutina reescribiendo el bloque Actor con la identidad de la
        /// sesion. Es la forma normal de llamar a cualquier rutina de negocio:
        /// todas empiezan por sigcm.paResolverActor y sin ese bloque fallan.
        /// </summary>
        protected IActionResult EjecutarConActor(string strRutina, string? strIpInput)
        {
            return Responder(strRutina, ConActor(strIpInput));
        }

        /// <summary>
        /// Igual que EjecutarConActor pero devuelve el JSON crudo. Solo para el
        /// puente de correo: SMTP no vive en SQL y hay que leer destinatarios
        /// que la rutina ya decidio antes de llamar a UT_Correo.
        /// </summary>
        protected string EjecutarPayloadConActor(string strRutina, string? strIpInput)
        {
            DaProceso daProceso = new DaProceso();
            return daProceso.ejecutarProceso(CONEXION, strRutina, ConActor(strIpInput));
        }

        private IActionResult Responder(string strRutina, string strParametro)
        {
            DaProceso _Daproceso = new DaProceso();
            string strPayload = _Daproceso.ejecutarProceso(CONEXION, strRutina, strParametro);

            try
            {
                // Se devuelve 200 aunque el payload traiga estado 0. El estado
                // del PROTOCOLO es correcto: la rutina respondio. El estado del
                // NEGOCIO viaja dentro, y el frontend lo lee de un solo lugar en
                // vez de repartirlo entre codigo HTTP y cuerpo.
                return Ok(JsonDocument.Parse(strPayload));
            }
            catch (JsonException)
            {
                // La rutina devolvio algo que no es JSON: incumple el contrato.
                // Es un fallo del servidor, no del cliente.
                return StatusCode(500, JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"CONTRATO\",\"mensaje\":" +
                    JsonSerializer.Serialize("La rutina " + strRutina + " devolvio una respuesta que no es JSON.") +
                    "}"));
            }
        }

        /// <summary>
        /// Devuelve el payload recibido con el bloque Actor sustituido por el de
        /// la sesion. Lo que el navegador haya mandado en Actor se descarta.
        /// </summary>
        private string ConActor(string? strIpInput)
        {
            JsonObject joPayload;

            try
            {
                joPayload = string.IsNullOrWhiteSpace(strIpInput)
                    ? new JsonObject()
                    : JsonNode.Parse(strIpInput) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                // Un payload ilegible se deja pasar vacio: la validacion de
                // entrada es de la rutina, no del puente, y su mensaje de error
                // es mas util que uno inventado aqui.
                joPayload = new JsonObject();
            }

            joPayload["Actor"] = ActorDeSesion();
            return joPayload.ToJsonString();
        }

        /// <summary>
        /// Traduce la sesion a la terna que exige sigcm.paResolverActor.
        ///
        /// El JWT lleva en el claim Name el payload completo de sesion, tal como
        /// lo entrega el SSO (o sigcm.paObtenerSesion en el ingreso local). De
        /// ahi salen la cuenta, el rol y la unidad; nunca del cuerpo de la
        /// peticion.
        /// </summary>
        private JsonObject ActorDeSesion()
        {
            string strUsuario = string.Empty;
            string strRol = string.Empty;
            string strUnidad = string.Empty;

            string? strSesion = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!string.IsNullOrWhiteSpace(strSesion))
            {
                try
                {
                    JsonNode? jnSesion = JsonNode.Parse(strSesion);
                    strUsuario = TextoNodo(jnSesion?["usuario"]);
                    if (string.IsNullOrWhiteSpace(strUsuario))
                        strUsuario = TextoNodo(jnSesion?["Usuario"]);

                    JsonNode? jnDetalle = jnSesion?["detalle"] is JsonArray
                        ? jnSesion["detalle"]![0]
                        : jnSesion?["detalle"];

                    // El token crudo del SSO a veces trae centro_costo y no
                    // cod_dependencia. paResolverActor acepta los dos.
                    strUnidad = PrimeroNoVacio(
                        TextoNodo(jnDetalle?["cod_dependencia"]),
                        TextoNodo(jnDetalle?["CodigoUnidad"]),
                        TextoNodo(jnDetalle?["centro_costo"]));

                    JsonNode? jnPerfil = jnDetalle?["perfil"] is JsonArray
                        ? jnDetalle!["perfil"]![0]
                        : jnDetalle?["perfil"];
                    strRol = PrimeroNoVacio(
                        TextoNodo(jnPerfil?["cod_perfil"]),
                        TextoNodo(jnPerfil?["CodigoRol"]));
                }
                catch (Exception)
                {
                    // Sesion con forma inesperada: se envia el actor vacio y la
                    // rutina responde VALIDACION_ACTOR, que es exactamente lo
                    // que ha pasado.
                }
            }

            return new JsonObject
            {
                ["Usuario"] = strUsuario,
                ["Rol"] = strRol,
                ["Unidad"] = strUnidad,
                ["Ip"] = UT_Host.GetClientIp(HttpContext),
                ["Equipo"] = Environment.MachineName,
                ["Programa"] = UT_Configuracion.AppSettings("appSettings", "programa") ?? "SIGCM-WEB",
                // Una correlacion por peticion: es lo que permite seguir en
                // sigcm.EventoAuditoria todo lo que provoco un solo clic.
                ["CorrelacionId"] = Guid.NewGuid().ToString()
            };
        }

        private static string PrimeroNoVacio(params string[] valores)
        {
            foreach (string valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor;
            }

            return string.Empty;
        }

        /// <summary>
        /// Lee un nodo JSON como texto aunque el SSO lo haya mandado como
        /// numero (el DNI a veces llega sin comillas). GetValue&lt;string&gt;
        /// en ese caso lanza y dejaba el actor vacio.
        /// </summary>
        private static string TextoNodo(JsonNode? nodo)
        {
            if (nodo is not JsonValue valor)
                return string.Empty;

            if (valor.TryGetValue(out string? texto))
                return texto?.Trim() ?? string.Empty;

            string crudo = valor.ToJsonString().Trim();
            return crudo.Trim('"');
        }
    }
}
