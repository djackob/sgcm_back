using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Tableros de seguimiento. Hoy, el de especialistas de Abastecimiento:
    /// lo ven el jefe y el coordinador (menu S051) y la rutina vuelve a
    /// validar el rol.
    /// </summary>
    [Authorize]
    public class DashboardController : ControladorPuente
    {
        /// <summary>
        /// Acciones, atendidos, despachados, pendientes, devoluciones, tiempo
        /// promedio y efectividad por especialista.
        /// Entrada: { "FechaDesde":"2026-01-01", "FechaHasta":"2026-09-27", "CodigoModulo":null }
        /// </summary>
        [HttpGet]
        public IActionResult resumenEspecialistas(string ipInput)
        {
            return EjecutarConActor("sigcm.paDashboardEspecialistas", ipInput);
        }
    }
}
