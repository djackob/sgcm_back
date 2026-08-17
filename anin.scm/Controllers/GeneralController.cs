using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
    }
}
