using System.Net;

namespace anin.util
{
    /// <summary>
    /// Consulta de RUC contra el bus de servicios institucional, que es quien
    /// habla con SUNAT. El SIGCM no consulta a SUNAT directamente: pega al mismo
    /// host que ya sirve RENIEC (wssg), con el mismo contrato.
    ///
    /// Gemelo de UT_Reniec y por la misma razon: ninguno de los dos parsea la
    /// respuesta. Devuelven el JSON tal como llega y quien lo pidio decide que
    /// hacer con el. Cuando el servicio responde algo que no es 200, se devuelve
    /// "{}" —un vacio valido— para que el llamador no tenga que distinguir entre
    /// "no hay JSON" y "no hay contribuyente".
    /// </summary>
    public class UT_Sunat
    {
        public static string ConsultaRuc(string strruc)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8602 // Desreferencia de una referencia posiblemente NULL.
                string strruta = UT_Configuracion.AppSettings("appSettings", "url_servicio_sunat").ToString() + "api/Servicio/ObtenerConsultaRuc?ipInput=" + strruc;
#pragma warning restore CS8602 // Desreferencia de una referencia posiblemente NULL.

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.GetAsync(strruta).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "{}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list = ex.Message;
            }

            return list;
        }
    }
}
