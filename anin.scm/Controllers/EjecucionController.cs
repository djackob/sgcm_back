using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [ejecucion]: modulo Ejecucion. Entrega de bienes o presentacion
    /// de entregables, verificacion, conformidad e inicio del pago.
    ///
    /// Declarado y vacio a proposito, igual que su esquema en la base (V001).
    /// Ver RequerimientoController para la pauta al implementarlo.
    /// </summary>
    [Authorize]
    public class EjecucionController : ControladorPuente
    {
    }
}
