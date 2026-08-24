using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [cmn]: modulo Gestion CMN. Solicitud (Anexo 3) y aprobacion
    /// (Anexo 4) de modificaciones al Cuadro Multianual de Necesidades, segun la
    /// Directiva N.° 0007-2025-EF/54.01.
    ///
    /// Las acciones del flujo (firmar, observar, derivar, validar, recepcionar)
    /// NO estan aqui: son transiciones de estado y viven en SigcmController,
    /// porque el motor es el mismo para todos los modulos. Aqui esta lo propio
    /// del CMN: la solicitud (Anexo 3) y el paquete (Anexo 4).
    /// </summary>
    [Authorize]
    public class CmnController : ControladorPuente
    {
        #region "Anexo 3 - Solicitud de modificacion del CMN"

        /// <summary>
        /// Bandeja del modulo. Por defecto devuelve "mi bandeja": lo que esta en
        /// la unidad del actor y cuyo estado tiene como responsable su rol, de
        /// modo que el especialista no ve lo que le toca firmar al jefe.
        ///
        /// Entrada: { "Filtro": { "SoloMiBandeja":true, "CodigoEstado":null,
        ///            "AnoEje":2026, "CentroCosto":null, "Texto":null,
        ///            "Limite":50, "Desplazamiento":0 } }
        /// Cada fila incluye Transiciones (acciones de este actor sobre ese
        /// expediente), para pintar los botones sin N llamadas extra.
        /// </summary>
        [HttpGet]
        public IActionResult listarSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paListarSolicitud", ipInput);
        }

        /// <summary>
        /// Solicitud completa: cabecera, items y los 48 periodos por item. Es lo
        /// que consume el visor del Anexo 3.
        /// Entrada: { "IdSolicitud":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paObtenerSolicitud", ipInput);
        }

        /// <summary>
        /// Registra el Anexo 3 con su sustento y sus items. Crea el expediente,
        /// la solicitud, los items y los 48 periodos de cada uno.
        ///
        /// Entrada: { "Solicitud": { "AnoEje":2026, "SecEjec":1750,
        ///              "CentroCosto":"01.01", "TipoOperacion":"MODIFICACION",
        ///              "Sustento":"..." },
        ///            "Items": [ { "TipoMovimiento":"INCLUSION", ... ,
        ///              "Periodos":[ {"AnoOffset":0,"Mes":1,"Cantidad":100} ] } ] }
        ///
        /// El cliente manda solo los meses con cantidad; la rutina materializa
        /// los 48 periodos rellenando con cero.
        /// </summary>
        [HttpPost]
        public IActionResult registrarSolicitud(string ipInput)
        {
            return EjecutarConActor("cmn.paRegistrarSolicitud", ipInput);
        }

        #endregion

        #region "Anexo 4 - Aprobacion de modificaciones del CMN"

        /// <summary>
        /// Reserva el Anexo 4 con uno o varios Anexos 3 ya aprobados, emite su
        /// codigo y devuelve el paquete para armar el PDF.
        ///
        /// Entrada: { "IdSolicitudes": ["...", "..."], "Sustento": null }
        /// </summary>
        [HttpPost]
        public IActionResult generarAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paGenerarAnexo4", ipInput);
        }

        /// <summary>
        /// El paquete completo, para armar o reconstruir el PDF.
        /// Entrada: { "IdPaquete": "..." } o { "IdSolicitud": "..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paObtenerAnexo4", ipInput);
        }

        /// <summary>
        /// Deshace un Anexo 4 que todavia no salio del especialista y libera
        /// sus Anexos 3.
        /// Entrada: { "IdPaquete": "...", "Motivo": "..." }
        /// </summary>
        [HttpPost]
        public IActionResult anularAnexo4(string ipInput)
        {
            return EjecutarConActor("cmn.paAnularAnexo4", ipInput);
        }

        #endregion
    }
}
