using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using anin.util;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [cmn]: modulo Gestion CMN. Solicitud (Anexo 3) y aprobacion
    /// (Anexo 4) de modificaciones al Cuadro Multianual de Necesidades, segun la
    /// Directiva N.° 0007-2025-EF/54.01.
    ///
    /// Las acciones del flujo (firmar, observar, derivar, validar, recepcionar)
    /// NO estan aqui: son transiciones de estado y viven en SigcmController,
    /// porque el motor es el mismo para todos los modulos. Aqui esta lo propio
    /// del CMN: la solicitud (Anexo 3) y el paquete (Anexo 4).
    /// </summary>
    [Authorize]
    public class CmnController : ControladorPuente
    {
        #region "Anexo 3 - Solicitud de modificacion del CMN"

        /// <summary>
        /// Bandeja del modulo. Por defecto devuelve "mi bandeja": lo que esta en
        /// la unidad del actor y cuyo estado tiene como responsable su rol, de
        /// modo que el especialista no ve lo que le toca firmar al jefe.
        ///
        /// Entrada: { "Filtro": { "SoloMiBandeja":true, "CodigoEstado":null,
        ///            "AnoEje":2026, "CentroCosto":null, "Texto":null,
        ///            "Limite":50, "Desplazamiento":0 } }
        /// Cada fila incluye Transiciones (acciones de este actor sobre ese
        /// expediente), para pintar los botones sin N llamadas extra.
        /// </summary>
        [HttpGet]
        public IActionResult listarSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paListarSolicitud", ipInput);
        }

        /// <summary>
        /// Solicitud completa: cabecera, items y los 48 periodos por item. Es lo
        /// que consume el visor del Anexo 3.
        /// Entrada: { "IdSolicitud":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paObtenerSolicitud", ipInput);
        }

        /// <summary>
        /// Registra el Anexo 3 con su sustento y sus items. Crea el expediente,
        /// la solicitud, los items y los 48 periodos de cada uno.
        ///
        /// Entrada: { "Solicitud": { "AnoEje":2026, "SecEjec":1750,
        ///              "CentroCosto":"01.01", "TipoOperacion":"MODIFICACION",
        ///              "Sustento":"..." },
        ///            "Items": [ { "TipoMovimiento":"INCLUSION", ... ,
        ///              "Periodos":[ {"AnoOffset":0,"Mes":1,"Cantidad":100} ] } ] }
        ///
        /// El cliente manda solo los meses con cantidad; la rutina materializa
        /// los 48 periodos rellenando con cero.
        /// </summary>
        [HttpPost]
        public IActionResult registrarSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paRegistrarSolicitud", ipInput);
        }

        #endregion

        #region "Anexo 4 - Aprobacion de modificaciones del CMN"

        /// <summary>
        /// Arma un Anexo 4 con uno o varios Anexos 3 ya aprobados por
        /// Abastecimiento, que pueden ser de areas usuarias distintas. Emite el
        /// codigo del Anexo 4 y reserva las solicitudes, de modo que ningun otro
        /// especialista pueda tomarlas mientras se arma el documento.
        ///
        /// Se llama ANTES de generar el PDF porque el codigo se imprime en el, y
        /// porque la regla de calendario —los Anexos 4 ordinarios salen los
        /// viernes— tiene que resolverse antes de subir nada.
        ///
        /// Entrada: { "IdSolicitudes": ["...","..."], "Sustento": null }
        /// </summary>
        [HttpPost]
        public IActionResult generarAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paGenerarAnexo4", ipInput);
        }

        /// <summary>
        /// El Anexo 4 completo, con sus solicitudes agrupadas por area usuaria y
        /// los items de cada una. Es lo que consume el visor y lo que permite
        /// reconstruir el PDF despues.
        ///
        /// Entrada: { "IdPaquete":"..." } o { "IdSolicitud":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paObtenerAnexo4", ipInput);
        }

        /// <summary>
        /// Deshace un Anexo 4 que todavia no salio del especialista y libera sus
        /// Anexos 3 para que puedan integrar otro.
        ///
        /// Entrada: { "IdPaquete":"...", "Motivo":"..." }
        /// </summary>
        [HttpPost]
        public IActionResult anularAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paAnularAnexo4", ipInput);
        }

        /// <summary>
        /// Avisa al area usuaria que su modificacion del CMN ya se hizo.
        ///
        /// La DERIVACION en el sistema no se hace aqui: la transicion
        /// CMN_ABAST_JEFE_FIRMAR_A4 deja el expediente en CMN_A4_ENVIADO, cuyo
        /// responsable es AREA_JEFE, y el enrutamiento de F004 lo devuelve a la
        /// unidad de origen. Esto es solo el correo, que es lo que faltaba: el
        /// area usuaria no vive dentro del sistema y su bandeja esta quieta la
        /// mayor parte del tiempo.
        ///
        /// Mismo reparto que la invitacion al locador: SMTP no corre en SQL, asi
        /// que la rutina arma el sobre, aqui se envia con el Anexo 4 adjunto y se
        /// vuelve a marcar el resultado. Si el correo falla NO se devuelve error:
        /// el expediente ya esta en la bandeja del area y la aprobacion en SIGA
        /// ya ocurrio; solo falto el aviso, y se puede reintentar.
        ///
        /// Se llama una vez por SOLICITUD: un Anexo 4 puede agrupar Anexos 3 de
        /// varias areas usuarias y cada una recibe el suyo.
        ///
        /// Entrada: { "IdSolicitud":"..." }
        /// </summary>
        [HttpPost]
        public IActionResult notificarAnexo4(string ipInput)
        {
            /* El destinatario lo decide la rutina leyendo sigcm.Usuario. Se pone
               al dia contra el SSO primero: si el jefe del area cambio su correo
               esta manana, el aviso tiene que ir al nuevo. */
            RefrescarPadronSso();

            string strSobre = EjecutarPayloadConActor(
                "cmn.paPrepararNotificacionAnexo4", ipInput);

            JsonNode? jnSobre;
            try
            {
                jnSobre = JsonNode.Parse(strSobre);
            }
            catch (JsonException)
            {
                return StatusCode(500, JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"CONTRATO\",\"mensaje\":\"La rutina de aviso del Anexo 4 devolvio una respuesta que no es JSON.\"}"));
            }

            if ((jnSobre?["estado"]?.GetValue<int>() ?? 0) != 1)
            {
                return Ok(JsonDocument.Parse(strSobre));
            }

            string strPara = jnSobre?["Destinatario"]?.GetValue<string>() ?? string.Empty;
            string? strCopia = jnSobre?["Copia"]?.GetValue<string>();
            string strAsunto = jnSobre?["Asunto"]?.GetValue<string>() ?? string.Empty;
            string strCuerpo = jnSobre?["Cuerpo"]?.GetValue<string>() ?? string.Empty;
            string? strAnexo4 = jnSobre?["Anexo4Documento"]?.GetValue<string>();
            string? strNombreA4 = jnSobre?["NombreAnexo4"]?.GetValue<string>();

            /* El Anexo 4 firmado va adjunto. Si el archivo no esta en el file
               server el aviso se manda igual: el texto ya dice lo importante y
               el documento sigue disponible en el expediente. */
            List<AdjuntoCorreo> adjuntos = new List<AdjuntoCorreo>();
            if (!string.IsNullOrWhiteSpace(strAnexo4)
                && UT_File.TryRutaFisica(strAnexo4, "cmn", out string strRuta))
            {
                adjuntos.Add(new AdjuntoCorreo
                {
                    Nombre = string.IsNullOrWhiteSpace(strNombreA4) ? strAnexo4 : strNombreA4,
                    Ruta = strRuta
                });
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

            joMarca["Destinatario"] = strPara;
            joMarca["Copia"] = strCopia;
            joMarca["ResultadoCorreo"] = strMsgCorreo;
            joMarca["CorreoEnviado"] = blEnviado;
            joMarca["Anexo4Documento"] = strAnexo4;

            string strMarca = EjecutarPayloadConActor(
                "cmn.paMarcarAnexo4Notificado", joMarca.ToJsonString());

            JsonNode? jnMarca;
            try
            {
                jnMarca = JsonNode.Parse(strMarca);
            }
            catch (JsonException)
            {
                jnMarca = JsonNode.Parse(
                    "{\"estado\":1,\"mensaje\":\"Se aviso al area usuaria. No se pudo leer la confirmacion de la rutina.\"}");
            }

            if (jnMarca is JsonObject joRespuesta)
            {
                joRespuesta["CorreoEnviado"] = blEnviado;
                joRespuesta["mensajeCorreo"] = strMsgCorreo;
                if (!blEnviado)
                {
                    joRespuesta["mensaje"] =
                        "El Anexo 4 quedo en la bandeja del area usuaria. El correo no se envio: " + strMsgCorreo;
                }
            }

            try
            {
                return Ok(JsonDocument.Parse(jnMarca?.ToJsonString() ?? strMarca));
            }
            catch (JsonException)
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":1,\"mensaje\":\"Se registro el aviso del Anexo 4.\"}"));
            }
        }

        #endregion
    }
}
