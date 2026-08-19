using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace anin.util
{
    public class UT_Configuracion
    {
        public static string? AppSettings(string strNodo, string strKey)
        {
            ConfigurationBuilder cbConfiguracion = new ConfigurationBuilder();
            string strRuta = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            string strRutaLocal = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Local.json");
            cbConfiguracion.AddJsonFile(strRuta, false);
            // Configuracion privada del equipo. Es opcional y se carga despues
            // para que pueda reemplazar rutas locales sin modificar ni subir
            // appsettings.json.
            cbConfiguracion.AddJsonFile(strRutaLocal, true);

            var bRoot = cbConfiguracion.Build();
            return bRoot.GetSection(strNodo).GetSection(strKey).Value;
        }

        public static string MensajeJson(string strMensaje)
        {
            string[] arrMensaje = strMensaje.Split('|');
            string strRespuesta = string.Concat("{\"estado\":", arrMensaje[0], ",\"mensaje\":");
            if (arrMensaje[0] == "1")
            {
                strRespuesta += string.Concat(arrMensaje[1], "}");
            }
            else
            {
                strRespuesta += string.Concat("\"", arrMensaje[1], "\"}");
            }
            return strRespuesta;
        }
    }
}
