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
        ///
        /// Para mover VARIOS expedientes con una sola accion —el Anexo 4 que
        /// agrupa Anexos 3 de varias areas usuarias— se manda IdExpedientes en
        /// lugar de IdExpediente:
        ///   { "IdExpedientes": [ {"IdExpediente":"...","Version":3},
        ///                        {"IdExpediente":"...","Version":7} ], ... }
        /// Se mueven todos en la misma transaccion: o avanzan todos o ninguno.
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

        #region "Documentos y firmas"

        /// <summary>
        /// Documentos del expediente con su version vigente: que hay, en que
        /// estado, con que URL se abre el PDF y si el rol que mira puede firmar.
        ///
        /// Entrada: { "IdExpediente":"..." }
        /// </summary>
        [HttpGet]
        public IActionResult listarDocumento(string ipInput)
        {
            return EjecutarConActor("sigcm.paListarDocumento", ipInput);
        }

        /// <summary>
        /// Registra el documento que el frontend genero y subio al file server.
        ///
        /// Entrada: { "IdExpediente":"...", "CodigoTipoDocumento":"...",
        ///            "GeneradoDocumento":"ID de documento_sistema", "NombreDocumento":"...",
        ///            "ArchivoHash":"...", "Payload":{ } }
        ///
        /// El orden importa: primero se sube el PDF por api/general/SubirArchivo
        /// y despues se registra aqui el documento_sistema que devolvio. Un
        /// documento sin archivo no se registra.
        ///
        /// Si la version vigente ya tenia firmas, la rutina crea una version
        /// nueva y las invalida todas: es la invalidacion de firma de CMN-18.
        ///
        /// Un Anexo 4 consolidado se registra UNA vez para los N expedientes que
        /// cubre, mandando IdExpedientes y su propio Numero:
        ///   { "IdExpedientes":["...","..."], "Numero":"A4-2026-000007", ... }
        /// El tipo de documento debe admitir consolidado; el Anexo 3, que es
        /// individual, no lo admite.
        /// </summary>
        [HttpPost]
        public IActionResult registrarDocumento(string ipInput)
        {
            return EjecutarConActor("sigcm.paRegistrarDocumento", ipInput);
        }

        /// <summary>
        /// Firma la version vigente del documento.
        ///
        /// Entrada: { "IdExpediente":"...", "CodigoTipoDocumento":"...",
        ///            "ArchivoHash":"...", "GeneradoDocumento":"..." }
        ///
        /// Quien puede firmar cada documento lo dice sigcm.TipoDocumentoFirma,
        /// que es dato sembrado. Este endpoint es tambien el punto de entrada
        /// del firmador institucional cuando se integre: recibira el PDF ya
        /// firmado y su huella, y nada mas del sistema cambiara.
        ///
        /// Los anexos llevan VARIAS firmas en cadena. Cada llamada registra la
        /// del rol que la hace; la version queda PARCIAL mientras falte alguna y
        /// pasa a FIRMADO con la ultima. La respuesta trae FirmasPendientes y la
        /// lista Pendientes para que la pantalla diga a quien le toca. Repetir la
        /// firma de un rol que ya firmo no es error: responde OK sin duplicarla.
        ///
        /// Firmar NO mueve el expediente. La accion del flujo que corresponde
        /// —CMN_FIRMAR_A3, REQ_FIRMAR_AU— se ejecuta despues por
        /// ejecutarTransicion, que comprueba que el documento este firmado.
        /// </summary>
        [HttpPost]
        public IActionResult firmarDocumento(string ipInput)
        {
            return EjecutarConActor("sigcm.paFirmarDocumento", ipInput);
        }

        #endregion
    }
}
