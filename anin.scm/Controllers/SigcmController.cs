using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace anin.scm.Controllers
{
    /// <summary>
    /// Esquema [sigcm]: nucleo transversal. Lo que no pertenece a un modulo
    /// concreto sino a todos: los maestros de SIGA que alimentan los
    /// formularios, la maquina de estados y la trazabilidad del expediente.
    ///
    /// Un controlador por esquema, y el nombre del endpoint es el verbo de la
    /// rutina sin el prefijo pa. Buscar de donde sale una pantalla es buscar el
    /// esquema, no leer codigo.
    /// </summary>
    [Authorize]
    public class SigcmController : ControladorPuente
    {
        #region "Maestros de SIGA"

        /// <summary>
        /// Punto unico de lectura de los maestros de SIGA para los formularios.
        /// Entrada: { "Maestro":"CATALOGO", "AnoEje":2026, "SecEjec":1750,
        ///            "CentroCosto":"01.01", "Texto":"...", "Limite":50 }
        /// Maestros validos: CENTRO_COSTO, META, FUENTE_FINANC, TAREA,
        /// UNIDAD_MEDIDA, CATALOGO, CUADRO_VIGENTE, TECHO, ETAPA_CENTRO.
        /// </summary>
        [HttpGet]
        public IActionResult listarMaestroSiga(string ipInput)
        {
            return EjecutarConActor("sigcm.paListarMaestroSiga", ipInput);
        }

        #endregion

        #region "Maquina de estados"

        /// <summary>
        /// Que puede hacer ESTE actor con ESTE expediente ahora mismo. Es lo que
        /// pinta los botones de accion de la bandeja.
        ///
        /// El frontend no deduce acciones a partir del estado: eso seria
        /// reimplementar la maquina de estados en TypeScript y esperar que las
        /// dos copias no se separen nunca.
        ///
        /// Entrada: { "IdExpediente":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult listarTransicionDisponible(string ipInput)
        {
            return EjecutarConActor("sigcm.paListarTransicionDisponible", ipInput);
        }

        /// <summary>
        /// Ejecuta una accion del flujo. La rutina valida rol, estado de origen y
        /// version antes de mover nada.
        ///
        /// Entrada: { "IdExpediente":"...", "CodigoTransicion":"CMN_ENVIAR_OA",
        ///            "Version":3, "Comentario":"...", "IdUnidadDestino":null,
        ///            "Datos":{ } }
        /// La Version es la que el cliente leyo: si otro usuario movio el
        /// expediente entretanto, la rutina responde CONFLICTO en vez de pisar
        /// el cambio.
        /// </summary>
        [HttpPost]
        public IActionResult ejecutarTransicion(string ipInput)
        {
            return EjecutarConActor("sigcm.paEjecutarTransicion", ipInput);
        }

        /// <summary>
        /// Historial, observaciones y cola de integracion de un expediente.
        /// Entrada: { "IdExpediente":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult obtenerTrazabilidad(string ipInput)
        {
            return EjecutarConActor("sigcm.paObtenerTrazabilidad", ipInput);
        }

        #endregion
    }
}
