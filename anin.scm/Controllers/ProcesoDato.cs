using anin.dataAccess;
using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace anin_scm.Controllers
{
    [Authorize]
    public class ProcesoDato : Controller
    {
        [HttpPost]
        public async Task<IActionResult> procesarAchivoReporteGasto(IFormFile file, int anio,string fecha_corte,string usuario_creacion)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Archivo vacío");

            var extension = Path.GetExtension(file.FileName).ToLower();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            List<Dictionary<string, object>> listRresultado;

            if (extension == ".xlsx")
            {
                listRresultado = UT_Excel.LeerXlsx(stream);
            }
            else if (extension == ".xls")
            {
                listRresultado = UT_Excel.LeerXls(stream);
            }
            else
            {
                return BadRequest("Formato no soportado. Solo .xls o .xlsx");
            }

            string strIp = UT_Host.GetClientIp(HttpContext);
            string strParametro = "{\"anio\": " + anio + ", \"fecha_corte\": \"" + fecha_corte + "\",\"usuario_proceso\":\"" + usuario_creacion + "\",\"ip\":\""+ strIp + "\",\"data\":" + JsonSerializer.Serialize(listRresultado, new JsonSerializerOptions()) + "}";
            DaProceso _Daproceso = new DaProceso();
            var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                "cnx_sgp", "sgp.fn_procesar_tm_sgp_gasto_siaf", strParametro));
            if (strResultado != null)
            {
                return Ok(strResultado);
            }
            else
            {
                return NotFound();
            }
        }
    }
}
