using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using anin.util;
using System.Collections.Generic;

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
    /// Alcance actual: registro y consulta (REQ-01 a REQ-14), mas indagacion
    /// de mercado (invitacion uno a uno al locador), filtros de idoneidad,
    /// CCP, orden de servicio y notificacion (ERF locacion).
    /// Las acciones del flujo siguen yendo por SigcmController / S003-S016.

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
        /// Invitacion uno a uno al locador del Anexo 5, al entrar a indagacion
        /// de mercado. SMTP no corre en SQL: la rutina arma el sobre y aqui se
        /// adjuntan A3/A6/A7/integridad. Si el correo falla se marca igual para
        /// no bloquear el expediente (homologacion / SMTP institucional).
        /// </summary>
        [HttpPost]
        public IActionResult invitacionCotizacionLocador(string ipInput)
        {
            string strSobre = EjecutarPayloadConActor(
                "requerimiento.paPrepararInvitacionLocador", ipInput);

            JsonNode? jnSobre;
            try
            {
                jnSobre = JsonNode.Parse(strSobre);
            }
            catch (JsonException)
            {
                return StatusCode(500, JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"CONTRATO\",\"mensaje\":\"La rutina de invitacion devolvio una respuesta que no es JSON.\"}"));
            }

            if ((jnSobre?["estado"]?.GetValue<int>() ?? 0) != 1)
            {
                return Ok(JsonDocument.Parse(strSobre));
            }

            string strPara = jnSobre?["Destinatario"]?.GetValue<string>() ?? string.Empty;
            string? strCopia = jnSobre?["Copia"]?.GetValue<string>();
            string strAsunto = jnSobre?["Asunto"]?.GetValue<string>() ?? string.Empty;
            string strCuerpo = jnSobre?["Cuerpo"]?.GetValue<string>() ?? string.Empty;

            List<AdjuntoCorreo> adjuntos = new List<AdjuntoCorreo>();
            try
            {
                JsonNode? jnEntrada = string.IsNullOrWhiteSpace(ipInput)
                    ? null
                    : JsonNode.Parse(ipInput);
                JsonArray? jaAdj = jnEntrada?["Adjuntos"] as JsonArray;
                if (jaAdj != null)
                {
                    foreach (JsonNode? jnAdj in jaAdj)
                    {
                        string strId = jnAdj?["DocumentoSistema"]?.GetValue<string>() ?? string.Empty;
                        string strNombre = jnAdj?["Nombre"]?.GetValue<string>() ?? strId;
                        string strCarpeta = jnAdj?["Carpeta"]?.GetValue<string>() ?? "requerimiento";
                        if (string.IsNullOrWhiteSpace(strId))
                        {
                            continue;
                        }
                        if (UT_File.TryRutaFisica(strId, strCarpeta, out string strRuta))
                        {
                            adjuntos.Add(new AdjuntoCorreo { Nombre = strNombre, Ruta = strRuta });
                        }
                    }
                }
            }
            catch (JsonException)
            {
                /* Sin adjuntos: el sobre igual se intenta enviar. */
            }

            string strEnvio = UT_Correo.envioCorreo("de", strPara, strAsunto, strCuerpo, strCopia, adjuntos);

            JsonNode? jnEnvio;
            try
            {
                jnEnvio = JsonNode.Parse(strEnvio);
            }
            catch (JsonException)
            {
                jnEnvio = JsonNode.Parse("{\"estado\":0,\"mensaje\":\"El envio de correo no devolvio JSON.\"}");
            }

            bool blEnviado = (jnEnvio?["estado"]?.GetValue<int>() ?? 0) == 1;
            string strMsgCorreo = jnEnvio?["mensaje"]?.GetValue<string>()
                ?? (blEnviado ? "Envio de correo satisfactorio" : "No se pudo enviar el correo.");

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

            joMarca["ResultadoCorreo"] = strMsgCorreo;
            joMarca["CorreoEnviado"] = blEnviado;
            joMarca["Destinatario"] = strPara;
            if (jnSobre?["PlazoHasta"] != null)
            {
                joMarca["PlazoHasta"] = jnSobre["PlazoHasta"]!.GetValue<string>();
            }

            JsonArray? jaEntrada = (string.IsNullOrWhiteSpace(ipInput) ? null : JsonNode.Parse(ipInput))?["Adjuntos"] as JsonArray;
            if (jaEntrada != null)
            {
                foreach (JsonNode? jnAdj in jaEntrada)
                {
                    string strTipo = jnAdj?["CodigoTipoDocumento"]?.GetValue<string>() ?? string.Empty;
                    string strId = jnAdj?["DocumentoSistema"]?.GetValue<string>() ?? string.Empty;
                    if (strTipo == "REQ_TDR_LOCACION") joMarca["Anexo3Documento"] = strId;
                    if (strTipo == "REQ_COTIZACION_ANEXO6") joMarca["Anexo6Documento"] = strId;
                    if (strTipo == "REQ_DJ_ANEXO7") joMarca["Anexo7Documento"] = strId;
                    if (strTipo == "REQ_PAQUETE_INTEGRIDAD") joMarca["IntegridadDocumento"] = strId;
                }
            }

            string strMarca = EjecutarPayloadConActor(
                "requerimiento.paMarcarInvitacionEnviada", joMarca.ToJsonString());

            JsonNode? jnMarca;
            try
            {
                jnMarca = JsonNode.Parse(strMarca);
            }
            catch (JsonException)
            {
                jnMarca = JsonNode.Parse(
                    "{\"estado\":1,\"mensaje\":\"Se registro la invitacion. No se pudo leer la confirmacion de la rutina.\"}");
            }

            if (jnMarca is JsonObject joRespuesta)
            {
                joRespuesta["CorreoEnviado"] = blEnviado;
                joRespuesta["mensajeCorreo"] = strMsgCorreo;
                joRespuesta["PlazoHasta"] = jnSobre?["PlazoHasta"]?.GetValue<string>();
                if (!blEnviado)
                {
                    joRespuesta["mensaje"] =
                        "Se inicio la indagacion de mercado. El correo institucional no se envio: " + strMsgCorreo;
                }
            }

            try
            {
                return Ok(JsonDocument.Parse(jnMarca?.ToJsonString() ?? strMarca));
            }
            catch (JsonException)
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":1,\"mensaje\":\"Se registro la invitacion de cotizacion.\"}"));
            }
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

            /* El alta SGCM-E no se hace aqui: debe pasar siempre por
               api/General/InsertarUsuarioExterno (validaciones propias del SSO).
               El front lo invoca antes de notificarOrdenServicio. */

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
