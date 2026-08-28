using anin.scm.Services;
using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace anin.scm.Controllers
{
    /// <summary>
    /// La puerta institucional: ingreso por el SSO de la ANIN.
    ///
    /// QUE CAMBIO Y POR QUE
    /// Antes, tksistema firmaba directamente lo que devolvia el servicio de
    /// token del SSO y eso era la sesion. Funcionaba mientras el SIGCM no
    /// necesitara mas que un nombre, pero cada rutina de negocio empieza por
    /// sigcm.paResolverActor, que exige la terna usuario-rol-unidad, y el sobre
    /// del SSO NO TRAE ni el centro de costo ni el codigo de perfil: eso vive en
    /// su base, no en su token.
    ///
    /// Ahora el ingreso pasa por SsoAccesoService, que valida el token igual que
    /// antes, trae el padron de la base del SSO, lo reconcilia contra DBSIGCM y
    /// arma la sesion con la MISMA rutina que usa el ingreso local. El sobre de
    /// respuesta es el de siempre -{estado, mensaje}- con un tercer estado nuevo,
    /// PERFIL, para cuando la persona ejerce mas de una terna y tiene que elegir.
    /// </summary>
    [AllowAnonymous]
    public class TokenController : ControllerBase
    {
        [HttpPost]
        public IActionResult tksistema(string strtoken)
        {
            string strResultado = SsoAccesoService.Ingresar(strtoken, Environment.MachineName);

            return Ok(JsonDocument.Parse(strResultado));
        }

        /// <summary>
        /// Segundo tramo del ingreso, solo cuando tksistema respondio PERFIL.
        ///
        /// Entrada: strpretoken, el pase que devolvio tksistema, e ipInput con
        /// { "CodigoRol":"...", "CodigoUnidad":"..." }.
        ///
        /// La cuenta NO se recibe: sale del pase. Aceptarla del cliente
        /// convertiria este endpoint en una puerta abierta a la sesion de
        /// cualquiera con solo saber su nombre de usuario.
        /// </summary>
        [HttpPost]
        public IActionResult iniciarSesionPerfil(string strpretoken, string ipInput)
        {
            string strResultado = SsoAccesoService.IniciarSesionConPerfil(
                strpretoken, ipInput, Environment.MachineName);

            return Ok(JsonDocument.Parse(strResultado));
        }

        /// <summary>
        /// Sincronizacion del padron a pedido. El ingreso ya sincroniza solo, asi
        /// que esto es para el mantenimiento: ver el resumen de altas, bajas y
        /// descartes sin esperar a que alguien entre.
        ///
        /// Exige sesion: es informacion del padron de la entidad, no una pantalla
        /// publica.
        /// </summary>
        [Authorize]
        [HttpPost]
        public IActionResult sincronizarPadronSso()
        {
            string strResultado = SsoAccesoService.SincronizarPadron(
                User?.Identity?.Name, Environment.MachineName);

            return Ok(JsonDocument.Parse(strResultado));
        }

        [HttpPost]
        public IActionResult tksistemaexterno(string strtoken)
        {
            string strResultado = UT_Sso.ValidarAccesoExterno(strtoken);
            StringBuilder sbResultado = new StringBuilder();
            if (strResultado.Equals("{}") || strResultado.Equals(""))
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "ERROR", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat("\"", "Usuario y/o Clave Incorrecta.", "\"}"));
            }
            else
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "OK", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat(TokenService.CreateToken(strResultado.ToString()), "}"));
            }

            return Ok(JsonDocument.Parse(sbResultado.ToString()));
        }

        [HttpGet]
        public IActionResult LoginOut()
        {
            string strResultado = UT_Sso.LoginOut();
            return Ok(JsonDocument.Parse(strResultado));
        }
    }
}
