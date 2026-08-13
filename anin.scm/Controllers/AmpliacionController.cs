using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [ampliacion]: modulo Modificacion-Ampliacion.
    ///
    /// Declarado y vacio a proposito, igual que su esquema en la base (V001).
    /// Ver RequerimientoController para la pauta al implementarlo.
    /// </summary>
    [Authorize]
    public class AmpliacionController : ControladorPuente
    {
    }
}
