using anin.dataAccess;
using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace anin.scm.Controllers
{
    [Authorize]
    public class GeneralController : ControllerBase
    {
        private static bool ServicioExternoHabilitado()
        {
            return string.Equals(UT_Configuracion.AppSettings("appSettings", "file_servicio_externo"),
                                 "true", StringComparison.OrdinalIgnoreCase);
        }

        [HttpPost]
        [RequestFormLimits(ValueCountLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue)]
        [DisableRequestSizeLimit]
        public IActionResult SubirArchivo(IFormFile? uploadFile, string strcarpeta)
        {
            try
            {
                IFormFile? archivo = uploadFile
                    ?? (HttpContext.Request.Form.Files.Count > 0 ? HttpContext.Request.Form.Files[0] : null);

                if (archivo == null)
                {
                    return Ok(JsonDocument.Parse(
                        "{\"estado\":0,\"mensaje\":\"No se recibio ningun archivo.\"," +
                        "\"documento_original\":\"\",\"documento_sistema\":\"\"}"));
                }

                string strPayload;

                if (ServicioExternoHabilitado())
                {
                    strPayload = UT_File.SubirArchivo(HttpContext);
                }
                else
                {
                    Stream sfile = archivo.OpenReadStream();
                    strPayload = UT_File.SubirArchivo(sfile, archivo.FileName, strcarpeta);
                }

                var strResultado = JsonDocument.Parse(strPayload);
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":0,\"mensaje\":" + JsonSerializer.Serialize(ex.Message) +
                    ",\"documento_original\":\"\",\"documento_sistema\":\"\"}"));
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult DescargarArchivo(string strarchivo, string? strcarpeta = null)
        {
            string strId = UT_File.IdDocumentoSistema(strarchivo);
            if (string.IsNullOrWhiteSpace(strId))
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":0,\"mensaje\":\"No se indico el archivo a descargar.\"}"));
            }

            if (UT_File.TryRutaFisica(strId, strcarpeta, out string strRutaFisica))
            {
                var tipos = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!tipos.TryGetContentType(strId, out string? strTipo) || string.IsNullOrEmpty(strTipo))
                {
                    strTipo = "application/octet-stream";
                }

                return PhysicalFile(strRutaFisica, strTipo);
            }

            if (ServicioExternoHabilitado())
            {
                var strResultado = JsonDocument.Parse(UT_File.DescargarArchivo(strId));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
            }

            return Ok(JsonDocument.Parse(
                "{\"estado\":0,\"mensaje\":\"No se encontro el archivo indicado.\"}"));
        }

        [HttpGet]
        public IActionResult ConsultaPersonaReniec(string ipInput)
        {
            try
            {
                var strResultado = JsonDocument.Parse(UT_Reniec.ConsultaPersonaReniec(ipInput));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }

                return NotFound();
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Razon social y estado de un contribuyente, por RUC. Es el gemelo de
        /// ConsultaPersonaReniec para el proveedor persona juridica: en el Anexo
        /// 5 el RUC se tecleaba entero y la razon social tambien, y los dos
        /// tienen que coincidir con SUNAT o la orden sale a nombre equivocado.
        ///
        /// Entrada: el RUC en crudo, igual que RENIEC recibe el DNI.
        /// </summary>
        [HttpGet]
        public IActionResult ConsultaRucSunat(string ipInput)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipInput))
                {
                    return BadRequest(JsonDocument.Parse(
                        "{\"estado\":0,\"mensaje\":\"Debe indicar el RUC.\"}"));
                }

                /* Se reenvia el JSON tal cual llega del bus. Volver a serializar
                   con Ok(JsonDocument) a veces deja un arbol que el front no
                   reconoce (mismo caso que RENIEC). */
                var json = UT_Sunat.ConsultaRuc(ipInput.Trim());
                if (string.IsNullOrWhiteSpace(json))
                {
                    json = "{\"strcodigo\":\"-1\",\"strnombres\":\"\","
                        + "\"strresultado\":\"Respuesta vacia del bus SUNAT.\"}";
                }

                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                string motivo = (ex.GetBaseException().Message ?? string.Empty);
                if (ex is TaskCanceledException
                    || ex is OperationCanceledException
                    || ex is TimeoutException
                    || motivo.IndexOf("E/S", StringComparison.OrdinalIgnoreCase) >= 0
                    || motivo.IndexOf("I/O", StringComparison.OrdinalIgnoreCase) >= 0
                    || motivo.IndexOf("anul", StringComparison.OrdinalIgnoreCase) >= 0
                    || motivo.IndexOf("canceled", StringComparison.OrdinalIgnoreCase) >= 0
                    || motivo.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    motivo = "SUNAT no respondió a tiempo. Intente nuevamente en unos momentos.";
                }
                else if (string.IsNullOrWhiteSpace(motivo))
                {
                    motivo = "Error al consultar SUNAT.";
                }

                motivo = motivo
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", " ")
                    .Replace("\n", " ");
                return Content(
                    "{\"strcodigo\":\"-1\",\"strnombres\":\"\",\"strresultado\":\""
                    + motivo + "\"}",
                    "application/json");
            }
        }

        #region "Ubigeo y usuario externo (SSO / general)"

        /// <summary>
        /// Departamentos INEI. general.fn_listar_departamento no recibe parametro.
        /// Devuelve [{ "iddpto":"15", "departamento":"LIMA" }, ...].
        /// </summary>
        [HttpGet]
        public IActionResult ListarDepartamento()
        {
            DaProcesoSso daSso = new DaProcesoSso();
            return Ok(daSso.EjecutarProceso(
                "SELECT general.fn_listar_departamento()::text;"));
        }

        /// <summary>
        /// Provincias del departamento. Entrada: { "iddpto":"15" } (iddpto de
        /// ListarDepartamento).
        /// </summary>
        [HttpGet]
        public IActionResult ListarProvincia(string ipInput)
        {
            DaProcesoSso daSso = new DaProcesoSso();
            return Ok(daSso.EjecutarProceso(
                "SELECT general.fn_listar_provincia($1::json)::text;",
                30,
                string.IsNullOrWhiteSpace(ipInput) ? "{}" : ipInput));
        }

        /// <summary>
        /// Distritos de la provincia. Entrada: { "idprov":"1501" } (idprov de
        /// ListarProvincia).
        /// </summary>
        [HttpGet]
        public IActionResult ListarDistrito(string ipInput)
        {
            DaProcesoSso daSso = new DaProcesoSso();
            return Ok(daSso.EjecutarProceso(
                "SELECT general.fn_listar_distrito($1::json)::text;",
                30,
                string.IsNullOrWhiteSpace(ipInput) ? "{}" : ipInput));
        }

        /// <summary>
        /// Alta (o reactivacion de acceso) del locador como usuario externo
        /// SGCM-E. login.fn_insertar_tm_login_usuario_externo_contrataciones.
        /// ipInput llega del front con la forma que pide la funcion.
        /// Punto unico de entrada: el flujo de notificacion O/S debe llamar
        /// este endpoint (no invocar la funcion SSO desde otro puente).
        ///
        /// Si la respuesta trae clave_inicial distinta de null, se envian las
        /// credenciales SSO (usuario + clave_inicial) al correo del payload.
        /// Si clave_inicial es null, el usuario ya tenia contraseña: no se envia
        /// correo de credenciales.
        /// </summary>
        [HttpPost]
        public IActionResult InsertarUsuarioExterno(string ipInput)
        {
            string strInput = string.IsNullOrWhiteSpace(ipInput) ? "{}" : ipInput;

            DaProcesoSso daSso = new DaProcesoSso();
            JsonDocument jdAlta = daSso.EjecutarProceso(
                "SELECT login.fn_insertar_tm_login_usuario_externo_contrataciones($1::json)::text;",
                30,
                strInput);

            JsonObject? joRespuesta;
            try
            {
                joRespuesta = JsonNode.Parse(jdAlta.RootElement.GetRawText()) as JsonObject;
            }
            catch (JsonException)
            {
                return Ok(jdAlta);
            }

            if (joRespuesta == null)
            {
                return Ok(jdAlta);
            }

            long idExterno = 0;
            if (joRespuesta["id_usuario_externo"] != null
                && joRespuesta["id_usuario_externo"]!.GetValueKind() != JsonValueKind.Null)
            {
                long.TryParse(joRespuesta["id_usuario_externo"]!.ToString(), out idExterno);
            }

            string? strClaveInicial = null;
            if (joRespuesta["clave_inicial"] != null
                && joRespuesta["clave_inicial"]!.GetValueKind() != JsonValueKind.Null)
            {
                strClaveInicial = joRespuesta["clave_inicial"]!.GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(strClaveInicial))
                {
                    strClaveInicial = null;
                }
            }

            /* Solo usuarios nuevos (clave_inicial informada) reciben el correo. */
            if (idExterno > 0 && !string.IsNullOrEmpty(strClaveInicial))
            {
                string strUsuario = joRespuesta["usuario"]?.GetValue<string>()?.Trim()
                    ?? string.Empty;
                string strCorreo = ExtraerCorreoUsuarioExterno(strInput);
                string strEnvio = EnviarCredencialesSso(strCorreo, strUsuario, strClaveInicial);

                JsonNode? jnEnvio = null;
                try
                {
                    jnEnvio = JsonNode.Parse(strEnvio);
                }
                catch (JsonException)
                {
                    jnEnvio = JsonNode.Parse(
                        "{\"estado\":0,\"mensaje\":\"El envio de credenciales no devolvio JSON.\"}");
                }

                joRespuesta["CredencialesEnviadas"] =
                    (jnEnvio?["estado"]?.GetValue<int>() ?? 0) == 1;
                joRespuesta["MensajeCorreoCredenciales"] =
                    jnEnvio?["mensaje"]?.GetValue<string>()
                    ?? "No se pudo interpretar la respuesta del correo.";
            }
            else
            {
                joRespuesta["CredencialesEnviadas"] = false;
                if (idExterno > 0)
                {
                    joRespuesta["MensajeCorreoCredenciales"] =
                        "El usuario ya tiene contraseña en el SSO; no se envian credenciales.";
                }
            }

            return Ok(JsonDocument.Parse(joRespuesta.ToJsonString()));
        }

        /// <summary>
        /// Correo del locador en el JSON de alta:
        /// { "correo": [ { "correo_electronico": "..." } ] }.
        /// </summary>
        private static string ExtraerCorreoUsuarioExterno(string strInput)
        {
            try
            {
                JsonNode? jn = JsonNode.Parse(strInput);
                JsonArray? ja = jn?["correo"] as JsonArray;
                if (ja == null || ja.Count == 0)
                {
                    return string.Empty;
                }

                string? str = ja[0]?["correo_electronico"]?.GetValue<string>();
                return string.IsNullOrWhiteSpace(str) ? string.Empty : str.Trim();
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string EnviarCredencialesSso(
            string strCorreo, string strUsuario, string strClaveInicial)
        {
            if (string.IsNullOrWhiteSpace(strCorreo))
            {
                return "{\"estado\":0,\"mensaje\":\"No hay correo del locador para enviar las credenciales SSO.\"}";
            }

            if (string.IsNullOrWhiteSpace(strUsuario) || string.IsNullOrWhiteSpace(strClaveInicial))
            {
                return "{\"estado\":0,\"mensaje\":\"Faltan usuario o clave_inicial para el correo de credenciales.\"}";
            }

            string strLink = UT_Configuracion.AppSettings("appSettings", "link_sistema")
                ?? "https://dsso.anin.gob.pe/login";

            string strAsunto = "Credenciales de acceso al portal del locador — SIGCM / ANIN";
            string strCuerpo = string.Concat(
                "<p>Se ha creado su acceso al sistema de contrataciones menores (SIGCM).</p>",
                "<p><b>Usuario:</b> ", System.Net.WebUtility.HtmlEncode(strUsuario), "</p>",
                "<p><b>Contraseña inicial:</b> ", System.Net.WebUtility.HtmlEncode(strClaveInicial), "</p>",
                "<p>Ingrese en: <a href=\"", System.Net.WebUtility.HtmlEncode(strLink), "\">",
                System.Net.WebUtility.HtmlEncode(strLink), "</a></p>",
                "<p>Por seguridad, cambie la contraseña tras el primer ingreso.</p>",
                "<p>Autoridad Nacional de Infraestructura — SIGCM</p>");

            return UT_Correo.envioCorreo("de", strCorreo, strAsunto, strCuerpo);
        }

        #endregion
    }
}
