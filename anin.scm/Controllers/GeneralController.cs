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
        public IActionResult SubirArchivo(string strcarpeta)
        {
            try
            {
                if (HttpContext.Request.Form.Files.Count == 0)
                {
                    return Ok(JsonDocument.Parse(
                        "{\"estado\":0,\"mensaje\":\"No se recibio ningun archivo.\"," +
                        "\"documento_original\":\"\",\"documento_sistema\":\"\"}"));
                }

                string strPayload;

                if (ServicioExternoHabilitado())
                {
                    // El servicio de la ANIN organiza por cod_file, asi que
                    // strcarpeta no viaja: alli la carpeta es DESARROLLO/SCM.
                    strPayload = UT_File.SubirArchivo(HttpContext);
                }
                else
                {
                    string strFileName = HttpContext.Request.Form.Files[0].FileName;
                    Stream sfile = HttpContext.Request.Form.Files[0].OpenReadStream();
                    strPayload = UT_File.SubirArchivo(sfile, strFileName, strcarpeta);
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
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult DescargarArchivo(string strarchivo, string? strcarpeta = null)
        {
            string? strUrlFile = UT_Configuracion.AppSettings("appSettings", "urlfile");

            if (string.IsNullOrEmpty(strUrlFile) || string.IsNullOrWhiteSpace(strarchivo))
            {
                return Ok(JsonDocument.Parse(
                    "{\"estado\":0,\"mensaje\":\"No se pudo resolver la ruta del archivo.\"}"));
            }

            // Lo que llega es un nombre guardado por el propio sistema, pero se
            // limpia igual: nunca se construye una ruta con texto del cliente
            // sin quitarle los saltos de carpeta.
            string strRelativa = strarchivo.Replace("\\", "/").Replace("..", "").TrimStart('/');
            string strSubcarpeta = (strcarpeta ?? string.Empty)
                .Replace("\\", "/").Replace("..", "").Trim('/');

            string strUrl = string.Concat(strUrlFile.TrimEnd('/'), "/",
                                          strSubcarpeta.Length == 0 ? "" : strSubcarpeta + "/",
                                          strRelativa);

            return Ok(JsonDocument.Parse(
                "{\"estado\":1,\"mensaje\":" + JsonSerializer.Serialize(strUrl) + "}"));
        }
    }
}
