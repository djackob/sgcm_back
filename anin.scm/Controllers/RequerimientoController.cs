using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using anin.util;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [requerimiento]: modulo Requerimiento a Notificacion. Desde el
    /// registro de la necesidad hasta la emision y notificacion de la orden.
    ///
    /// Las acciones del flujo —derivar al Jefe, firmar el documento tecnico,
    /// remitir a OA o a DAI, observar, declarar conforme— NO estan aqui: son
    /// transiciones de estado y se ejecutan por SigcmController, con la
    /// configuracion que siembra S003. Aqui solo esta lo propio del modulo.
    ///
    /// Alcance actual: registro y consulta (REQ-01 a REQ-14), mas filtros de
    /// idoneidad, CCP, orden de servicio y notificacion (ERF locacion).
    /// Las acciones del flujo siguen yendo por SigcmController / S003-S004.

    /// </summary>
    [Authorize]
    public class RequerimientoController : ControladorPuente
    {
        #region "Registro de la necesidad"

        /// <summary>
        /// Bandeja del modulo. Por defecto devuelve "mi bandeja": lo que esta en
        /// la unidad del actor y cuyo estado tiene como responsable su rol.
        ///
        /// Entrada: { "Filtro": { "SoloMiBandeja":true, "CodigoEstado":null,
        ///            "AnoEje":2026, "CentroCosto":null,
        ///            "CodigoTipoContratacion":null, "Texto":null,
        ///            "Limite":50, "Desplazamiento":0 } }
        /// Cada fila incluye Transiciones (acciones de este actor sobre ese
        /// expediente), para pintar los botones sin N llamadas extra.
        /// </summary>
        [HttpGet]
        public IActionResult listarRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paListarRequerimiento", ipInput);
        }

        /// <summary>
        /// Requerimiento completo: cabecera, pedidos SIGA e items. Es lo que
        /// consume el visor y el formulario mientras esta editable (REQ-11).
        ///
        /// Entrada: { "IdRequerimiento":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paObtenerRequerimiento", ipInput);
        }

        /// <summary>
        /// Registra la necesidad con sus pedidos SIGA y sus items.
        ///
        /// Entrada: { "Requerimiento": { … }, "Pedidos": [ … ], "Items": [ … ] }
        ///
        /// La rutina valida el tope de ocho UIT del anio, la condicion frente al
        /// CMN, los diez dias habiles de antelacion y que la suma de los items
        /// coincida con el monto declarado. Ninguna de esas reglas se replica
        /// aqui: el mensaje de la rutina dice exactamente que fallo.
        /// </summary>
        [HttpPost]
        public IActionResult registrarRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paRegistrarRequerimiento", ipInput);
        }

        #endregion

        #region "Locacion: filtros, CCP, orden y correo"

        [HttpGet]
        public IActionResult listarFiltroIdoneidad(string ipInput)
        {
            return EjecutarConActor("requerimiento.paListarFiltroIdoneidad", ipInput);
        }

        [HttpPost]
        public IActionResult registrarFiltroIdoneidad(string ipInput)
        {
            return EjecutarConActor("requerimiento.paRegistrarFiltroIdoneidad", ipInput);
        }

        [HttpPost]
        public IActionResult confirmarFiltrosIdoneidad(string ipInput)
        {
            return EjecutarConActor("requerimiento.paConfirmarFiltrosIdoneidad", ipInput);
        }

        [HttpPost]
        public IActionResult derivarFiltrosIdoneidad(string ipInput)
        {
            return EjecutarConActor("requerimiento.paDerivarFiltrosIdoneidad", ipInput);
        }

        [HttpPost]
        public IActionResult registrarCcp(string ipInput)
        {
            return EjecutarConActor("requerimiento.paRegistrarCcp", ipInput);
        }

        [HttpPost]
        public IActionResult registrarOrdenServicio(string ipInput)
        {
            return EjecutarConActor("requerimiento.paRegistrarOrdenServicio", ipInput);
        }

        /// <summary>
        /// Unica excepcion al puente de una linea: SMTP no corre en SQL.
        /// La rutina arma destinatario, copia, asunto y cuerpo; aqui solo se
        /// llama a UT_Correo.envioCorreo y se marca el envio.
        /// </summary>
        [HttpPost]
        public IActionResult notificarOrdenServicio(string ipInput)
        {
            string strSobre = EjecutarPayloadConActor(
                "requerimiento.paPrepararNotificacionOrden", ipInput);

            JsonNode? jnSobre;
            try
            {
                jnSobre = JsonNode.Parse(strSobre);
            }
            catch (JsonException)
            {
                return StatusCode(500, JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"CONTRATO\",\"mensaje\":\"La rutina de notificacion devolvio una respuesta que no es JSON.\"}"));
            }

            if ((jnSobre?["estado"]?.GetValue<int>() ?? 0) != 1)
            {
                return Ok(JsonDocument.Parse(strSobre));
            }

            string strPara = jnSobre?["Destinatario"]?.GetValue<string>() ?? string.Empty;
            string? strCopia = jnSobre?["Copia"]?.GetValue<string>();
            string strAsunto = jnSobre?["Asunto"]?.GetValue<string>() ?? string.Empty;
            string strCuerpo = jnSobre?["Cuerpo"]?.GetValue<string>() ?? string.Empty;

            string strEnvio = UT_Correo.envioCorreo("de", strPara, strAsunto, strCuerpo, strCopia);

            JsonNode? jnEnvio;
            try
            {
                jnEnvio = JsonNode.Parse(strEnvio);
            }
            catch (JsonException)
            {
                jnEnvio = JsonNode.Parse("{\"estado\":0,\"mensaje\":\"El envio de correo no devolvio JSON.\"}");
            }

            if ((jnEnvio?["estado"]?.GetValue<int>() ?? 0) != 1)
            {
                return Ok(JsonDocument.Parse(strEnvio));
            }

            JsonObject joMarca;
            try
            {
                joMarca = string.IsNullOrWhiteSpace(ipInput)
                    ? new JsonObject()
                    : JsonNode.Parse(ipInput) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                joMarca = new JsonObject();
            }

            joMarca["ResultadoCorreo"] = jnEnvio?["mensaje"]?.GetValue<string>() ?? "Envio de correo satisfactorio";

            string strMarca = EjecutarPayloadConActor(
                "requerimiento.paMarcarOrdenNotificada", joMarca.ToJsonString());

            try
            {
                return Ok(JsonDocument.Parse(strMarca));
            }
            catch (JsonException)
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":1,\"mensaje\":\"El correo se envio. No se pudo leer la confirmacion de la rutina.\"}"));
            }
        }

        #endregion
    }
}
