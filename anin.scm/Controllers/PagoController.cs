using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [pago]: modulo Pago. Conformidad, check list (Anexo 9),
    /// penalidades (Anexo 10), control previo, devengado y giro.
    ///
    /// Declarado y vacio a proposito, igual que su esquema en la base (V001).
    /// Ver RequerimientoController para la pauta al implementarlo.
    /// </summary>
    [Authorize]
    public class PagoController : ControladorPuente
    {
    }
}
