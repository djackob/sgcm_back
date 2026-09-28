using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [reporte]: reportes transversales para Abastecimiento. El
    /// expediente documental se busca por la orden de compra o de servicio y
    /// recorre toda su contratacion (CMN, requerimiento, ejecucion,
    /// modificacion, resolucion y pagos). Solo los perfiles ABAST_.
    /// </summary>
    [Authorize]
    public class ReporteController : ControladorPuente
    {
        /// <summary>
        /// Ordenes que coinciden con el numero (o el codigo del requerimiento).
        /// Entrada: { "Filtro":{ "Texto":"...", "TipoOrden":"OC|OS", "AnoEje":2026, "Limite":15 } }
        /// </summary>
        [HttpGet]
        public IActionResult buscarOrden(string ipInput)
        {
            return EjecutarConActor("reporte.paBuscarOrden", ipInput);
        }

        /// <summary>
        /// Ficha de la orden, cadena de expedientes, documentos e historial.
        /// Entrada: { "IdOrdenServicio":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerExpedienteDocumental(string ipInput)
        {
            return EjecutarConActor("reporte.paObtenerExpedienteDocumental", ipInput);
        }
    }
}
