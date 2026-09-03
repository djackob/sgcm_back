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
    }
}
