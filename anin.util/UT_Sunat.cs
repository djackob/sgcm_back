using System.Net;

namespace anin.util
{
    /// <summary>
    /// Consulta de RUC contra el bus de servicios institucional (wssg), el mismo
    /// host que RENIEC. El SIGCM no habla con SUNAT directamente.
    ///
    /// Contrato de respuesta (texto JSON):
    /// - Exito: strcodigo "1" y strnombres con la razon social.
    /// - No existe: strcodigo "0" y strnombres vacio (lo que el bus ya manda).
    /// - Fallo de red/config/HTTP/timeout: strcodigo "-1" y strresultado con el
    ///   motivo, para que el front no diga "no se encontro" cuando en realidad
    ///   no pudo consultar.
    /// </summary>
    public class UT_Sunat
    {
        public static string ConsultaRuc(string strruc)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string? baseUrl = UT_Configuracion.AppSettings("appSettings", "url_servicio_sunat");
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return ErrorJson("No esta configurada url_servicio_sunat.");
                }

                if (!baseUrl.EndsWith('/'))
                {
                    baseUrl += "/";
                }

                string strruta = baseUrl
                    + "api/Servicio/ObtenerConsultaRuc?ipInput="
                    + Uri.EscapeDataString(strruc ?? string.Empty);

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                        HttpResponseMessage response = httpClient.GetAsync(strruta)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult();

                        if (!response.IsSuccessStatusCode)
                        {
                            return ErrorJson("El bus SUNAT respondio HTTP "
                                + (int)response.StatusCode + ".");
                        }

                        string cuerpo = response.Content.ReadAsStringAsync()
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult();

                        return string.IsNullOrWhiteSpace(cuerpo) ? "{}" : cuerpo;
                    }
                }
            }
            catch (Exception ex)
            {
                return ErrorJson(MensajeAmigable(ex));
            }
        }

        private static string MensajeAmigable(Exception ex)
        {
            Exception raiz = ex.GetBaseException() ?? ex;
            string motivo = raiz.Message ?? string.Empty;

            if (ex is TaskCanceledException
                || ex is OperationCanceledException
                || ex is TimeoutException
                || raiz is TaskCanceledException
                || raiz is OperationCanceledException
                || raiz is TimeoutException
                || motivo.IndexOf("E/S", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("I/O", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("canceled", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("anul", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || motivo.IndexOf("tiempo de espera", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "SUNAT no respondio a tiempo. Intente nuevamente en unos momentos.";
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                return "No fue posible consultar SUNAT.";
            }

            return motivo;
        }

        private static string ErrorJson(string mensaje)
        {
            string seguro = (mensaje ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
            return "{\"strcodigo\":\"-1\",\"strnombres\":\"\",\"strresultado\":\""
                + seguro + "\"}";
        }
    }
}
