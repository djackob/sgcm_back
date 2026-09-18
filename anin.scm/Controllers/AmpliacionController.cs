using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [ampliacion]: modulo Modificacion-Ampliacion (Directiva
    /// 002-2026-ANIN 7.3.4 y 7.3.5). Solicitudes de modificacion del contrato
    /// y de ampliacion de plazo sobre un contrato en ejecucion.
    ///
    /// Las acciones del flujo son transiciones de sigcm (S039); pasan por las
    /// rutinas del modulo porque cada una graba lo suyo -opinion, decision,
    /// acta- y resuelve a que unidad va la solicitud. El correo al proveedor
    /// (7.3.5.4) usa el puente generico de ControladorPuente.
    /// </summary>
    [Authorize]
    public class AmpliacionController : ControladorPuente
    {
        /// <summary>Bandeja. Entrada: { "Filtro": { "Tipo", "Texto", "CodigoEstado", "SoloVigentes", "IdContrato", "Limite", "Desplazamiento" } }</summary>
        [HttpGet]
        public IActionResult listarSolicitud(string ipInput)
        {
            return EjecutarConActor("ampliacion.paListarSolicitud", ipInput);
        }

        /// <summary>Detalle. Entrada: { "IdSolicitud" } o { "IdExpediente" }</summary>
        [HttpGet]
        public IActionResult obtenerSolicitud(string ipInput)
        {
            return EjecutarConActor("ampliacion.paObtenerSolicitud", ipInput);
        }

        /// <summary>Nueva solicitud. Entrada: { "IdContrato", "Tipo", "Asunto", "Sustento", "SolicitudDocumento", "FechaFinHechoGenerador", "DiasSolicitados", "DetalleModificacion" }</summary>
        [HttpPost]
        public IActionResult registrarSolicitud(string ipInput)
        {
            return EjecutarConActor("ampliacion.paRegistrarSolicitud", ipInput);
        }

        /// <summary>Opinion del area usuaria. Entrada: { "IdExpediente", "Version", "Resultado": "PROCEDE|NO_PROCEDE", "Informe", "InformeDocumento", "DetalleModificacion" }</summary>
        [HttpPost]
        public IActionResult opinarAu(string ipInput)
        {
            return EjecutarConActor("ampliacion.paOpinarAu", ipInput);
        }

        /// <summary>Decision de la DEC. Entrada: { "IdExpediente", "Version", "Resultado": "APROBADA|DENEGADA", "Motivo", "DiasOtorgados", "CartaDocumento", "NumeroCarta" }</summary>
        [HttpPost]
        public IActionResult decidirDec(string ipInput)
        {
            return EjecutarConActor("ampliacion.paDecidirDec", ipInput);
        }

        /// <summary>Numero y archivo del acta de modificacion. Entrada: { "IdExpediente", "NumeroActa", "ActaDocumento", "RegistroPladicop" }</summary>
        [HttpPost]
        public IActionResult registrarActa(string ipInput)
        {
            return EjecutarConActor("ampliacion.paRegistrarActa", ipInput);
        }

        /// <summary>Transiciones sin datos propios: remitir, firmar acta, suscribir. Entrada: { "IdExpediente", "Version", "CodigoTransicion", "Comentario" }</summary>
        [HttpPost]
        public IActionResult ejecutarAccion(string ipInput)
        {
            return EjecutarConActor("ampliacion.paEjecutarAccion", ipInput);
        }

        /// <summary>Notifica la decision al proveedor por correo institucional (7.3.5.4). Entrada: { "IdExpediente" }</summary>
        [HttpPost]
        public IActionResult notificarDecision(string ipInput)
        {
            return NotificarPorCorreo("ampliacion.paPrepararNotificacion", "ampliacion.paMarcarNotificada", ipInput);
        }

        /// <summary>Registra una notificacion hecha fuera del sistema (mesa de partes, Pladicop). Entrada: { "IdExpediente", "MedioNotificacion", "ResultadoCorreo" }</summary>
        [HttpPost]
        public IActionResult marcarNotificada(string ipInput)
        {
            return EjecutarConActor("ampliacion.paMarcarNotificada", ipInput);
        }
    }
}
