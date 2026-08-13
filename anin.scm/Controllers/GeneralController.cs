using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Servicios transversales que no pertenecen a ningun esquema de la base:
    /// el manejo de archivos del file server.
    ///
    /// POR QUE NO HEREDA DE ControladorPuente
    /// No llama a ninguna rutina de base de datos. Aqui no hay JSON de entrada
    /// ni bloque Actor: hay un archivo que llega por multipart y se escribe en
    /// disco. Es la unica parte del backend que no es un puente, y lo es porque
    /// el sistema de archivos no esta dentro de la base.
    ///
    /// QUE SE GUARDA Y QUE NO
    /// Aqui solo se guarda el ARCHIVO. Su vinculo con el expediente —que este
    /// PDF es el Anexo 3 version 1 de tal solicitud— lo registra la base, con
    /// las rutinas del modulo. Este controlador devuelve la URL y nada mas.
    /// </summary>
    [Authorize]
    public class GeneralController : ControllerBase
    {
        /// <summary>
        /// Sube un archivo al file server, dentro de la subcarpeta indicada.
        ///
        /// Entrada: multipart con el archivo y strcarpeta ("cmn", "requerimiento").
        /// Salida:  { "estado":1, "documento_original":"...", "documento_sistema":"URL" }
        ///
        /// Es el endpoint que consume app-input-archivos a traves de
        /// MaestraService.subirArchivo, y tambien el que usa el frontend para
        /// depositar los anexos que genera.
        ///
        /// Los limites de tamanio se levantan porque un expediente puede traer
        /// planos o expedientes tecnicos escaneados; el tope real lo pone
        /// MAX_SIZE_UPLOAD, que el frontend valida antes de enviar.
        /// </summary>
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

                string strFileName = HttpContext.Request.Form.Files[0].FileName;
                Stream sfile = HttpContext.Request.Form.Files[0].OpenReadStream();

                var strResultado = JsonDocument.Parse(UT_File.SubirArchivo(sfile, strFileName, strcarpeta));
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

        /// <summary>
        /// Resuelve la URL publica de un archivo ya guardado.
        ///
        /// Entrada: strarchivo, la ruta relativa dentro del file server
        ///          ("cmn/20260813...pdf").
        /// Salida:  { "estado":1, "mensaje":"https://.../files/cmn/...pdf" }
        ///
        /// Es la via de compatibilidad para input-archivos: cuando lo que tiene
        /// guardado es un codigo y no una URL, pregunta aqui. En el SIGCM
        /// documento_sistema ya es una URL completa y el componente la abre
        /// directo, asi que este endpoint casi no se usa.
        /// </summary>
        [HttpGet]
        public IActionResult DescargarArchivo(string strarchivo)
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

            string strUrl = string.Concat(strUrlFile.TrimEnd('/'), "/", strRelativa);

            return Ok(JsonDocument.Parse(
                "{\"estado\":1,\"mensaje\":" + JsonSerializer.Serialize(strUrl) + "}"));
        }
    }
}
