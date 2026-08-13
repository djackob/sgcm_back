using anin.util;
using Microsoft.AspNetCore.Mvc;

namespace anin_scm.Controllers
{
    public class TestController : Controller
    {
        [HttpGet]
        public IActionResult TestConexion([FromHeader] string apiClave)
        {
            try
            {
                if (apiClave.Equals(UT_Configuracion.AppSettings("appSettings:testConexion", "clave")))
                {
                    var resultado = new
                    {
                        estado = 1,
                        fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        mensaje = UT_Configuracion.AppSettings("appSettings:testConexion", "mensaje_autorizado"),
                        sistema = UT_Configuracion.AppSettings("appSettings:testConexion", "sistema"),
                        plataforma = UT_Configuracion.AppSettings("appSettings:testConexion", "plataforma"),
                        version = UT_Configuracion.AppSettings("appSettings:testConexion", "version")
                    };
                    return Ok(resultado);
                }
                else
                {
                    return Unauthorized(new
                    {
                        estado = 0,
                        mensaje = UT_Configuracion.AppSettings("appSettings:testConexion", "mensaje_no_autorizado")
                    });
                }
            }
            catch (Exception ex)
            {
                return Unauthorized(new
                {
                    estado = 0,
                    mensaje = UT_Configuracion.AppSettings("appSettings:testConexion", "mensaje_no_autorizado")
                });
            }

        }

    }
}
