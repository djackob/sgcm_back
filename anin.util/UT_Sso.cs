using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace anin.util
{
    public class UT_Sso
    {
        public static string ValidarAcceso(string token)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8602 // Desreferencia de una referencia posiblemente NULL.
                string strruta = UT_Configuracion.AppSettings("appSettings", "url_token").ToString() + "api/Token/validartoken";
                string strcod_sistema = UT_Configuracion.AppSettings("appSettings", "cod_sistema_interno").ToString();
#pragma warning restore CS8602 // Desreferencia de una referencia posiblemente NULL.
                string data = string.Format("{0}{1}{2}{3}", "token=", token, "&cod_sistema=", strcod_sistema);
                StringContent queryString = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                using (var handler = new HttpClientHandler())
                {

                    // allow the bad certificate
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.PostAsync(strruta, queryString).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "-1";
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

        public static string ValidarAccesoExterno(string token)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8602 // Desreferencia de una referencia posiblemente NULL.
                string strruta = UT_Configuracion.AppSettings("appSettings", "url_token").ToString() + "api/Token/validartokenexterno";
                string strcod_sistema = UT_Configuracion.AppSettings("appSettings", "cod_sistema_externo").ToString();
#pragma warning restore CS8602 // Desreferencia de una referencia posiblemente NULL.
                string data = string.Format("{0}{1}{2}{3}", "token=", token, "&cod_sistema=", strcod_sistema);
                StringContent queryString = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                using (var handler = new HttpClientHandler())
                {

                    // allow the bad certificate
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.PostAsync(strruta, queryString).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "-1";
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

        public static string LoginOut()
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8602 // Desreferencia de una referencia posiblemente NULL.
                string strruta = UT_Configuracion.AppSettings("appSettings", "url_token").ToString() + "api/servicio/LoginOut";
#pragma warning restore CS8602 // Desreferencia de una referencia posiblemente NULL.

                using (var handler = new HttpClientHandler())
                {
                    // allow the bad certificate
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
                            list = "-1";
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
