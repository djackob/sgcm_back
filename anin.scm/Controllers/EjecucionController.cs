using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [ejecucion]: ejecucion contractual (Directiva 002-2026-ANIN 7.3).
    /// El contrato en ejecucion que nace con la orden notificada, las entregas
    /// fisicas de bienes (7.3.6.3) con sus dos rutas -Almacen o sede
    /// desconcentrada- y las incidencias que el AU comunica a la DEC (7.3.3).
    ///
    /// Las acciones del flujo son transiciones de sigcm (S038). Pasan por las
    /// rutinas del modulo y no por SigcmController.ejecutarTransicion porque
    /// cada una graba lo suyo -guia, verificador, acta, Pecosa- y resuelve a
    /// que unidad queda la entrega. La rama de servicios (presentar el
    /// entregable, conformidad, Anexo 11) es PagoController.
    /// </summary>
    [Authorize]
    public class EjecucionController : ControladorPuente
    {
        /// <summary>Bandeja de contratos. Entrada: { "Filtro": { "Texto", "CodigoEstado", "AnoEje", "SoloVigentes", "Limite", "Desplazamiento" } }</summary>
        [HttpGet]
        public IActionResult listarContrato(string ipInput)
        {
            return EjecutarConActor("ejecucion.paListarContrato", ipInput);
        }

        /// <summary>Detalle: contrato, entregas, entregables de pago, incidencias, plazos. Entrada: { "IdContrato" } o { "IdExpediente" }</summary>
        [HttpGet]
        public IActionResult obtenerContrato(string ipInput)
        {
            return EjecutarConActor("ejecucion.paObtenerContrato", ipInput);
        }

        /// <summary>Personas del area usuaria del contrato, para designar al verificador. Entrada: { "IdContrato" }</summary>
        [HttpGet]
        public IActionResult listarVerificadorDisponible(string ipInput)
        {
            return EjecutarConActor("ejecucion.paListarVerificadorDisponible", ipInput);
        }

        /// <summary>Abre (o confirma) el contrato de una orden ya notificada. Entrada: { "IdRequerimiento" }</summary>
        [HttpPost]
        public IActionResult abrirContrato(string ipInput)
        {
            return EjecutarConActor("ejecucion.paAbrirContrato", ipInput);
        }

        /// <summary>Lugar y direccion de entrega, supervisor del AU. Entrada: { "IdContrato", "LugarEntrega", "DireccionEntrega", "IdSupervisor" }</summary>
        [HttpPost]
        public IActionResult actualizarContrato(string ipInput)
        {
            return EjecutarConActor("ejecucion.paActualizarContrato", ipInput);
        }

        /// <summary>El proveedor anuncia una entrega de bienes con su guia. Entrada: { "IdContrato", "NumeroEntregable", "Detalle", "FechaPrevista", "NumeroGuiaRemision", "GuiaDocumento" }</summary>
        [HttpPost]
        public IActionResult anunciarEntrega(string ipInput)
        {
            return EjecutarConActor("ejecucion.paAnunciarEntrega", ipInput);
        }

        /// <summary>Autoriza el ingreso fisico (Almacen o AU segun la ruta). Entrada: { "IdExpediente", "Version" }</summary>
        [HttpPost]
        public IActionResult autorizarIngreso(string ipInput)
        {
            return EjecutarConActor("ejecucion.paAutorizarIngreso", ipInput);
        }

        /// <summary>El jefe del AU designa quien acompana la verificacion. Entrada: { "IdExpediente", "Version", "IdVerificador" }</summary>
        [HttpPost]
        public IActionResult designarVerificador(string ipInput)
        {
            return EjecutarConActor("ejecucion.paDesignarVerificador", ipInput);
        }

        /// <summary>Resultado de la verificacion. Entrada: { "IdExpediente", "Version", "Resultado": "CONFORME|OBSERVADO", "Detalle", "GuiaSuscritaDocumento", "ActaIncumplimientoDocumento" }</summary>
        [HttpPost]
        public IActionResult verificarEntrega(string ipInput)
        {
            return EjecutarConActor("ejecucion.paVerificarEntrega", ipInput);
        }

        /// <summary>Almacen entrega el bien al AU con Pecosa. Entrada: { "IdExpediente", "Version", "NumeroPecosa", "PecosaDocumento" }</summary>
        [HttpPost]
        public IActionResult entregarBienAu(string ipInput)
        {
            return EjecutarConActor("ejecucion.paEntregarBienAu", ipInput);
        }

        /// <summary>Transiciones de la entrega sin datos propios (registrar guia en Almacen, confirmar retiro). Entrada: { "IdExpediente", "Version", "CodigoTransicion", "Comentario" }</summary>
        [HttpPost]
        public IActionResult ejecutarAccionEntrega(string ipInput)
        {
            return EjecutarConActor("ejecucion.paEjecutarAccionEntrega", ipInput);
        }

        /// <summary>El AU comunica una incidencia, incumplimiento o riesgo (7.3.3). Entrada: { "IdContrato", "Tipo", "Detalle", "DocumentoSgd", "InformeDocumento" }</summary>
        [HttpPost]
        public IActionResult registrarIncidencia(string ipInput)
        {
            return EjecutarConActor("ejecucion.paRegistrarIncidencia", ipInput);
        }

        /// <summary>La DEC atiende la incidencia. Entrada: { "IdIncidencia", "Respuesta" }</summary>
        [HttpPost]
        public IActionResult atenderIncidencia(string ipInput)
        {
            return EjecutarConActor("ejecucion.paAtenderIncidencia", ipInput);
        }

        /// <summary>Culmina el contrato cuando todos los entregables tienen conformidad. Entrada: { "IdExpediente", "Version" }</summary>
        [HttpPost]
        public IActionResult culminarContrato(string ipInput)
        {
            return EjecutarConActor("ejecucion.paCulminarContrato", ipInput);
        }
    }
}
