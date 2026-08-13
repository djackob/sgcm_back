using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text;

namespace anin.util
{
    public class UT_File
    {
        public static string SubirArchivo(HttpContext hcbArchivo)
        {
            string list = string.Empty;
            string strFileName = string.Empty;
            string strFileNew = string.Empty;
            try
            {
                strFileName = hcbArchivo.Request.Form.Files[0].FileName;
                string strruta = string.Format("{0}{1}", UT_Configuracion.AppSettings("appSettings", "url_servicio"), "api/file/SubirArchivo");
#pragma warning disable CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                string strcodfile = UT_Configuracion.AppSettings("appSettings", "cod_file");
#pragma warning restore CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                var content = new MultipartFormDataContent();
                var fileStreamContent = new StreamContent(hcbArchivo.Request.Form.Files[0].OpenReadStream());
                content.Add(fileStreamContent, strFileName, strFileName);
#pragma warning disable CS8604 // Posible argumento de referencia nulo
                content.Add(new StringContent(strcodfile), "strcodfile");
#pragma warning restore CS8604 // Posible argumento de referencia nulo
                using (var handler = new HttpClientHandler())
                {
                    // allow the bad certificate
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.PostAsync(strruta, content).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "{\"estado\":0,\"mensaje\":\"Error al Subir el Archivo o Documento\",\"documento_original\":\"" + strFileName + "\",\"documento_sistema\":\"" + strFileNew + "\"}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list = list = "{\"estado\":0,\"mensaje\":\"" + ex.Message + "\",\"documento_original\":\"" + strFileName + "\",\"documento_sistema\":\"" + strFileNew + "\"}";
            }
            return list;
        }

        public static string DescargarArchivo(string strarchivo)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#pragma warning disable CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                string strcodfile = UT_Configuracion.AppSettings("appSettings", "cod_file");
                string strurl = UT_Configuracion.AppSettings("appSettings", "url_servicio") + "api/file/DescargarArchivo?strcodfile=" + strcodfile + "&strarchivo=" + strarchivo;

#pragma warning restore CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                using (var handler = new HttpClientHandler())
                {
                    // allow the bad certificate
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.GetAsync(strurl).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "{\"estado\":0,\"mensaje\":\"Error al Descargar Archivo\"}";
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                list = "{\"estado\":0,\"mensaje\":\"" + ex.Message + "\"}";
            }
            return list;
        }

        public static string DescargarImgTo64Bits(string strurl)
        {
            string list = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var handler = new HttpClientHandler())
                {
                    // allow the bad certificate
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.GetAsync(strurl).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            list = Convert.ToBase64String(response.Content.ReadAsByteArrayAsync().Result);
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

        public static string LeerArchivoTexto(string strRutaArchivo)
        {
            string strResultado = string.Empty;
            try
            {
                strResultado = File.ReadAllText(strRutaArchivo, Encoding.Default);
            }
            catch (Exception ex)
            {
                strResultado = ex.Message;
            }
            return strResultado;
        }

        public static string SubirArchivoRuta(string strcarpeta,string strarchivo)
        {
            string list = string.Empty;
            string strFileName = string.Empty;
            string strFileNew = string.Empty;
            try
            {
                //strFileName = hcbArchivo.Request.Form.Files[0].FileName;
                string strruta = string.Format("{0}{1}", UT_Configuracion.AppSettings("appSettings", "url_servicio"), "api/file/SubirArchivo");
#pragma warning disable CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                string strcodfile = UT_Configuracion.AppSettings("appSettings", "cod_file");
#pragma warning restore CS8600 // Se va a convertir un literal nulo o un posible valor nulo en un tipo que no acepta valores NULL
                FileStream fsDocumento = new FileStream(strcarpeta + strarchivo, FileMode.OpenOrCreate);
                var content = new MultipartFormDataContent();
                var fileStreamContent = new StreamContent(fsDocumento);
                content.Add(fileStreamContent, strarchivo, strarchivo);
#pragma warning disable CS8604 // Posible argumento de referencia nulo
                content.Add(new StringContent(strcodfile), "strcodfile");
#pragma warning restore CS8604 // Posible argumento de referencia nulo
                using (var handler = new HttpClientHandler())
                {
                    // allow the bad certificate
                    handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true;
                    using (var httpClient = new HttpClient(handler))
                    {
                        HttpResponseMessage response = httpClient.PostAsync(strruta, content).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            list = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            list = "{\"estado\":0,\"mensaje\":\"Error al Subir el Archivo o Documento\",\"documento_original\":\"" + strFileName + "\",\"documento_sistema\":\"" + strFileNew + "\"}";
                        }
                    }
                }

                fsDocumento.Dispose();
                fsDocumento.Close();
            }
            catch (Exception ex)
            {
                list = list = "{\"estado\":0,\"mensaje\":\"" + ex.Message + "\",\"documento_original\":\"" + strFileName + "\",\"documento_sistema\":\"" + strFileNew + "\"}";
            }
            return list;
        }

        public static string DescargarArchivoUrl(string fileUrl, string strRutaDestino) 
        {
            DateTime hoy = DateTime.Now;
            string strFilePdfDestino = hoy.Year.ToString() + hoy.Month.ToString().ToString() + hoy.Day.ToString() + hoy.Hour.ToString() + hoy.Minute.ToString() + hoy.Second.ToString() + hoy.Millisecond.ToString() + ".pdf";
            strRutaDestino = strRutaDestino + strFilePdfDestino;

            HttpClient httpClient = new HttpClient();
            var fileStream = Task.Run(async () => await httpClient.GetStreamAsync(fileUrl)).Result;

            using (MemoryStream ms = new MemoryStream())
            {
                fileStream.CopyTo(ms);

                using (FileStream fsFile = new FileStream(strRutaDestino, FileMode.Create, FileAccess.Write))
                {
                    ms.WriteTo(fsFile);
                }
            }

            return strFilePdfDestino;
        }

        public static string SubirArchivoTemp(HttpContext hcbArchivo, string strcarpeta)
        {
            string list = string.Empty;
            var file = hcbArchivo.Request.Form.Files;
            string strFileName = file[0].FileName;
            string strFileExtension = Path.GetExtension(strFileName).ToLower();
            DateTime hoy = DateTime.Now;
            string strFileNew = hoy.Year.ToString() + hoy.Month.ToString().ToString() + hoy.Day.ToString() + hoy.Hour.ToString() + hoy.Minute.ToString() + hoy.Second.ToString() + hoy.Millisecond.ToString();
            strFileNew = strFileNew + strFileExtension;
            string strfile = string.Format("{0}{1}", strcarpeta, strFileNew);
            using (var fileStream = new FileStream(strfile, FileMode.Create))
            {
                file[0].CopyTo(fileStream);
            }

            return strFileNew;
        }
    }
}