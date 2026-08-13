using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [resolucion]: modulo Resolucion. Un solo modulo con bandeja
    /// comun; el tipo de resolucion es un dato del expediente, no un submodulo.
    ///
    /// Declarado y vacio a proposito, igual que su esquema en la base (V001).
    /// Ver RequerimientoController para la pauta al implementarlo.
    /// </summary>
    [Authorize]
    public class ResolucionController : ControladorPuente
    {
    }
}
