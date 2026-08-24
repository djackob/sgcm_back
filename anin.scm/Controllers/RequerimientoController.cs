using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [requerimiento]: modulo Requerimiento a Notificacion. Desde el
    /// registro de la necesidad hasta la emision y notificacion de la orden.
    ///
    /// Las acciones del flujo —derivar al Jefe, firmar el documento tecnico,
    /// remitir a OA o a DAI, observar, declarar conforme— NO estan aqui: son
    /// transiciones de estado y se ejecutan por SigcmController, con la
    /// configuracion que siembra S003. Aqui solo esta lo propio del modulo.
    ///
    /// Alcance actual: registro y consulta (REQ-01 a REQ-14). La indagacion de
    /// mercado, el cuadro de cotizaciones, la CCP y la orden llegan con V010.
    /// </summary>
    [Authorize]
    public class RequerimientoController : ControladorPuente
    {
        #region "Registro de la necesidad"

        /// <summary>
        /// Bandeja del modulo. Por defecto devuelve "mi bandeja": lo que esta en
        /// la unidad del actor y cuyo estado tiene como responsable su rol.
        ///
        /// Entrada: { "Filtro": { "SoloMiBandeja":true, "CodigoEstado":null,
        ///            "AnoEje":2026, "CentroCosto":null,
        ///            "CodigoTipoContratacion":null, "Texto":null,
        ///            "Limite":50, "Desplazamiento":0 } }
        /// Cada fila incluye Transiciones (acciones de este actor sobre ese
        /// expediente), para pintar los botones sin N llamadas extra.
        /// </summary>
        [HttpGet]
        public IActionResult listarRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paListarRequerimiento", ipInput);
        }

        /// <summary>
        /// Requerimiento completo: cabecera, pedidos SIGA e items. Es lo que
        /// consume el visor y el formulario mientras esta editable (REQ-11).
        ///
        /// Entrada: { "IdRequerimiento":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paObtenerRequerimiento", ipInput);
        }

        /// <summary>
        /// Registra la necesidad con sus pedidos SIGA y sus items.
        ///
        /// Entrada: { "Requerimiento": { … }, "Pedidos": [ … ], "Items": [ … ] }
        ///
        /// La rutina valida el tope de ocho UIT del anio, la condicion frente al
        /// CMN, los diez dias habiles de antelacion y que la suma de los items
        /// coincida con el monto declarado. Ninguna de esas reglas se replica
        /// aqui: el mensaje de la rutina dice exactamente que fallo.
        /// </summary>
        [HttpPost]
        public IActionResult registrarRequerimiento(string ipInput)
        {
            return EjecutarConActor("requerimiento.paRegistrarRequerimiento", ipInput);
        }

        #endregion
    }
}
