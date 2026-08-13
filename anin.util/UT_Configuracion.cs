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
            cbConfiguracion.AddJsonFile(strRuta, false);

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
