using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [requerimiento]: modulo Requerimiento a Notificacion.
    ///
    /// Declarado y vacio a proposito, igual que su esquema en la base (V001):
    /// la estructura del sistema queda dicha desde el principio para que el
    /// modulo aterrice en su sitio sin discusion. Sus rutinas ocuparan el bloque
    /// de errores 51400-51499 (F005).
    ///
    /// Al implementarlo: un endpoint por rutina, mismo nombre sin el prefijo pa,
    /// heredando de ControladorPuente. Las acciones del flujo NO se agregan
    /// aqui: son transiciones y se ejecutan por SigcmController.
    /// </summary>
    [Authorize]
    public class RequerimientoController : ControladorPuente
    {
    }
}
