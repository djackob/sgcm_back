using System.Net;

namespace anin.util
{
    public class UT_Reniec
    {
        public static string ConsultaPersonaReniec(string strdni)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8602 // Desreferencia de una referencia posiblemente NULL.
                string strruta = UT_Configuracion.AppSettings("appSettings", "url_servicio_reniec").ToString() + "api/Servicio/ObtenerConsultaReniec?ipInput=" + strdni;
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
