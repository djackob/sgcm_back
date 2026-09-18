using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [resolucion]: modulo Resolucion del contrato menor (Directiva
    /// 002-2026-ANIN 7.3.7). Una maquina con dos entradas -el area usuaria
    /// informa la causal; el proveedor solicita- y el desvio del
    /// apercibimiento para el incumplimiento (7.3.7.2).
    ///
    /// Las acciones del flujo son transiciones de sigcm (S040) y pasan por las
    /// rutinas del modulo: guardan pronunciamientos, cartas y plazos, y al
    /// firmar la carta de resolucion cierran el contrato en Ejecucion.
    /// </summary>
    [Authorize]
    public class ResolucionController : ControladorPuente
    {
        /// <summary>Bandeja. Entrada: { "Filtro": { "Causal", "Texto", "CodigoEstado", "SoloVigentes", "IdContrato", "Limite", "Desplazamiento" } }</summary>
        [HttpGet]
        public IActionResult listarProcedimiento(string ipInput)
        {
            return EjecutarConActor("resolucion.paListarProcedimiento", ipInput);
        }

        /// <summary>Detalle. Entrada: { "IdProcedimiento" } o { "IdExpediente" }</summary>
        [HttpGet]
        public IActionResult obtenerProcedimiento(string ipInput)
        {
            return EjecutarConActor("resolucion.paObtenerProcedimiento", ipInput);
        }

        /// <summary>Inicio del procedimiento. Entrada: { "IdContrato", "Causal", "Alcance", "ParteResuelta", "Hechos", "Documento" }</summary>
        [HttpPost]
        public IActionResult registrarProcedimiento(string ipInput)
        {
            return EjecutarConActor("resolucion.paRegistrarProcedimiento", ipInput);
        }

        /// <summary>Pronunciamiento del area usuaria. Entrada: { "IdExpediente", "Version", "Pronunciamiento": "FAVORABLE|DESFAVORABLE", "Informe", "InformeDocumento" }</summary>
        [HttpPost]
        public IActionResult pronunciarAu(string ipInput)
        {
            return EjecutarConActor("resolucion.paPronunciarAu", ipInput);
        }

        /// <summary>Decision de la DEC. Entrada: { "IdExpediente", "Version", "Decision": "APERCIBIR|RESOLVER|DESESTIMAR", "Motivo", "PlazoApercibimientoDias", "Alcance", "ParteResuelta" }</summary>
        [HttpPost]
        public IActionResult decidirDec(string ipInput)
        {
            return EjecutarConActor("resolucion.paDecidirDec", ipInput);
        }

        /// <summary>Numero, archivo y medio de una carta. Entrada: { "IdExpediente", "TipoCarta": "APERCIBIMIENTO|RESOLUCION|RESPUESTA", "NumeroCarta", "CartaDocumento", "MedioNotificacion", "RegistroPladicop" }</summary>
        [HttpPost]
        public IActionResult registrarCarta(string ipInput)
        {
            return EjecutarConActor("resolucion.paRegistrarCarta", ipInput);
        }

        /// <summary>Remitir, firmar cartas, responder, vencer el plazo. Entrada: { "IdExpediente", "Version", "CodigoTransicion", "Comentario", "Respuesta", "RespuestaDocumento" }</summary>
        [HttpPost]
        public IActionResult ejecutarAccion(string ipInput)
        {
            return EjecutarConActor("resolucion.paEjecutarAccion", ipInput);
        }

        /// <summary>Notifica la carta al proveedor por correo (7.3.7.3). Entrada: { "IdExpediente" }</summary>
        [HttpPost]
        public IActionResult notificarCarta(string ipInput)
        {
            return NotificarPorCorreo("resolucion.paPrepararNotificacion", "resolucion.paMarcarNotificada", ipInput);
        }

        /// <summary>Registra una notificacion notarial o por Pladicop hecha fuera del sistema. Entrada: { "IdExpediente", "MedioNotificacion", "ResultadoCorreo" }</summary>
        [HttpPost]
        public IActionResult marcarNotificada(string ipInput)
        {
            return EjecutarConActor("resolucion.paMarcarNotificada", ipInput);
        }
    }
}
