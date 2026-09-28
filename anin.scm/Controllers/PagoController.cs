using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [pago]: entregables, conformidad (Anexo 11), checklist (Anexo 9),
    /// penalidades (Anexo 10), control previo, devengado y giro.
    ///
    /// Las acciones del flujo que son transiciones simples van por
    /// SigcmController.ejecutarTransicion. Aqui solo lo que la maquina no cubre:
    /// abrir el expediente desde la O/S, validar la carga del locador, calcular
    /// atraso y penalidad, y capturar numeros SIAF.
    /// </summary>
    [Authorize]
    public class PagoController : ControladorPuente
    {
        [HttpGet]
        public IActionResult listarPago(string ipInput)
        {
            return EjecutarConActor("pago.paListarPago", ipInput);
        }

        [HttpGet]
        public IActionResult obtenerPago(string ipInput)
        {
            return EjecutarConActor("pago.paObtenerPago", ipInput);
        }

        [HttpGet]
        public IActionResult listarPortalLocador(string ipInput)
        {
            return EjecutarConActor("pago.paListarPortalLocador", ipInput);
        }

        /// <summary>
        /// Lee de SIGA el estado real de la orden de servicio: si esta emitida,
        /// si tiene compromiso SIAF y con que expediente. Actualiza los hitos 1
        /// y 4. Entrada: { "IdExpediente": "..." }.
        /// </summary>
        [HttpGet]
        public IActionResult sincronizarOrdenSiga(string ipInput)
        {
            return EjecutarConActor("pago.paSincronizarOrdenSiga", ipInput);
        }

        [HttpPost]
        public IActionResult abrirExpedientePago(string ipInput)
        {
            return EjecutarConActor("pago.paAbrirExpedientePago", ipInput);
        }

        [HttpPost]
        public IActionResult presentarEntregable(string ipInput)
        {
            return EjecutarConActor("pago.paPresentarEntregable", ipInput);
        }

        [HttpPost]
        public IActionResult observarEntregable(string ipInput)
        {
            return EjecutarConActor("pago.paObservarEntregable", ipInput);
        }

        [HttpPost]
        public IActionResult otorgarVistoBueno(string ipInput)
        {
            return EjecutarConActor("pago.paOtorgarVistoBueno", ipInput);
        }

        [HttpPost]
        public IActionResult aprobarConformidadTecnica(string ipInput)
        {
            return EjecutarConActor("pago.paAprobarConformidadTecnica", ipInput);
        }

        [HttpPost]
        public IActionResult marcarConformidadFirmada(string ipInput)
        {
            return EjecutarConActor("pago.paMarcarConformidadFirmada", ipInput);
        }

        [HttpPost]
        public IActionResult registrarChecklist(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarChecklist", ipInput);
        }

        [HttpPost]
        public IActionResult liquidarExpediente(string ipInput)
        {
            return EjecutarConActor("pago.paLiquidarExpediente", ipInput);
        }

        [HttpPost]
        public IActionResult registrarDevengado(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarDevengado", ipInput);
        }

        [HttpPost]
        public IActionResult registrarGiro(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarGiro", ipInput);
        }

        [HttpPost]
        public IActionResult registrarProrroga(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarProrroga", ipInput);
        }

        /// <summary>
        /// El jefe o la secretaria del area asignan el entregable recibido a un
        /// especialista. Entrada: { IdExpediente, Version, IdResponsableDestino }.
        /// </summary>
        [HttpPost]
        public IActionResult asignarEspecialista(string ipInput)
        {
            return EjecutarConActor("pago.paAsignarEspecialista", ipInput);
        }

        /// <summary>
        /// Abastecimiento notifica al proveedor la observacion del area usuaria.
        /// </summary>
        [HttpPost]
        public IActionResult notificarObservacion(string ipInput)
        {
            return EjecutarConActor("pago.paNotificarObservacion", ipInput);
        }

        [HttpPost]
        public IActionResult actualizarNumeroContrato(string ipInput)
        {
            return EjecutarConActor("pago.paActualizarNumeroContrato", ipInput);
        }

        [HttpPost]
        public IActionResult registrarDocumentoAdicional(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarDocumentoAdicional", ipInput);
        }

        [HttpPost]
        public IActionResult anularDocumentoAdicional(string ipInput)
        {
            return EjecutarConActor("pago.paAnularDocumentoAdicional", ipInput);
        }

        /// <summary>Campanita y tablero de alertas del modulo.</summary>
        [HttpGet]
        public IActionResult resumenAlertas(string ipInput)
        {
            return EjecutarConActor("pago.paResumenAlertas", ipInput);
        }

        /// <summary>
        /// Correos enviados del expediente y de su requerimiento. Con IdCorreo
        /// devuelve el cuerpo para reconstruirlo como evidencia.
        /// </summary>
        [HttpGet]
        public IActionResult listarCorreo(string ipInput)
        {
            return EjecutarConActor("sigcm.paListarCorreo", ipInput);
        }

        /// <summary>
        /// Constancia de prestacion de la orden. Con Emitir=true la crea cuando
        /// todos los entregables estan pagados; sin el, solo informa.
        /// Entrada: { "IdExpediente":"...", "Emitir":true }
        /// </summary>
        [HttpPost]
        public IActionResult emitirConstancia(string ipInput)
        {
            return EjecutarConActor("pago.paEmitirConstancia", ipInput);
        }

        /// <summary>Guarda el PDF generado de la constancia.</summary>
        [HttpPost]
        public IActionResult registrarConstanciaDocumento(string ipInput)
        {
            return EjecutarConActor("pago.paRegistrarConstanciaDocumento", ipInput);
        }

        /// <summary>Envia la constancia al proveedor con el PDF adjunto.</summary>
        [HttpPost]
        public IActionResult notificarConstancia(string ipInput)
        {
            return NotificarPorCorreo("pago.paPrepararNotificacionConstancia", "pago.paMarcarConstanciaNotificada", ipInput);
        }
    }
}
