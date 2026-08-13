using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [integracion]: cola hacia SIGA, mapeo de identificadores y
    /// conciliacion. Autoridad compartida.
    ///
    /// SIN ENDPOINTS TODAVIA, Y NO ES UN OLVIDO
    /// La cola no se despacha desde una pantalla: la llena la transicion que la
    /// encola (CMN_VALIDAR_UA y CMN_FIRMAR_A4) y la vacia el worker de
    /// integracion, que corre con cuenta tecnica y sin usuario delante. Lo unico
    /// que hoy ve el usuario de esta cola es el bloque Integracion que devuelve
    /// sigcm.paObtenerTrazabilidad.
    ///
    /// Cuando W001 salga del modo simulacion (ADR-003) y haga falta reintentar o
    /// consultar operaciones desde la pantalla de administracion, los endpoints
    /// entran aqui y no en el controlador del modulo que las genero.
    /// </summary>
    [Authorize]
    public class IntegracionController : ControladorPuente
    {
    }
}
