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

        /// <summary>
        /// Guarda un archivo en el file server local y devuelve su URL publica.
        ///
        /// DIFERENCIA CON LAS OTRAS SOBRECARGAS
        /// SubirArchivo(HttpContext) publica hacia un servicio de archivos
        /// externo (url_servicio + cod_file). Esta guarda en el file server
        /// propio, el de appSettings:rutafile, que es como opera el SIGCM.
        ///
        /// EL NOMBRE SE REEMPLAZA, SIEMPRE
        /// El archivo se guarda con un nombre generado y nunca con el que trae
        /// el usuario. Dos motivos: dos personas suben "Anexo 3.pdf" el mismo
        /// dia y uno pisaria al otro, y un nombre venido del navegador puede
        /// contener separadores de ruta y escapar de la carpeta. El nombre
        /// original viaja aparte, en documento_original, para mostrarlo.
        ///
        /// Respuesta, en el formato que espera app-input-archivos:
        ///   { "estado":1, "mensaje":"...",
        ///     "documento_original":"Anexo 3.pdf",
        ///     "documento_sistema":"https://.../files/cmn/20260813...pdf" }
        /// </summary>
        /// <param name="stArchivo">Contenido del archivo.</param>
        /// <param name="strNombreOriginal">Nombre con el que llego; solo se usa para la extension y la respuesta.</param>
        /// <param name="strCarpeta">Subcarpeta bajo rutafile: "cmn", "requerimiento".</param>
        public static string SubirArchivo(Stream stArchivo, string strNombreOriginal, string strCarpeta)
        {
            string strFileNew = string.Empty;

            try
            {
                string? strRutaFile = UT_Configuracion.AppSettings("appSettings", "rutafile");
                string? strUrlFile = UT_Configuracion.AppSettings("appSettings", "urlfile");

                if (string.IsNullOrEmpty(strRutaFile) || string.IsNullOrEmpty(strUrlFile))
                {
                    return Respuesta(0, "No estan configurados appSettings:rutafile y appSettings:urlfile.",
                                     strNombreOriginal, string.Empty);
                }

                // Solo el nombre del archivo: descarta cualquier ruta que venga
                // en el nombre original.
                strNombreOriginal = Path.GetFileName(strNombreOriginal ?? string.Empty);

                // La carpeta la fija el frontend, asi que se limpia: sin unidades,
                // sin rutas absolutas y sin subir de nivel con "..".
                strCarpeta = (strCarpeta ?? string.Empty).Replace("\\", "/").Trim('/');
                if (strCarpeta.Contains("..") || Path.IsPathRooted(strCarpeta))
                {
                    return Respuesta(0, "La carpeta indicada no es valida.", strNombreOriginal, string.Empty);
                }

                string strExtension = Path.GetExtension(strNombreOriginal).ToLower();
                DateTime dtHoy = DateTime.Now;
                strFileNew = string.Concat(
                    dtHoy.ToString("yyyyMMddHHmmssfff"),
                    Guid.NewGuid().ToString("N").Substring(0, 8),
                    strExtension);

                string strCarpetaFisica = Path.Combine(strRutaFile, strCarpeta.Replace("/", "\\"));
                Directory.CreateDirectory(strCarpetaFisica);

                string strRutaCompleta = Path.Combine(strCarpetaFisica, strFileNew);

                using (FileStream fsDestino = new FileStream(strRutaCompleta, FileMode.Create, FileAccess.Write))
                {
                    stArchivo.CopyTo(fsDestino);
                }

                string strUrl = string.Concat(strUrlFile.TrimEnd('/'), "/",
                                              string.IsNullOrEmpty(strCarpeta) ? "" : strCarpeta + "/",
                                              strFileNew);

                return Respuesta(1, "Se subio el archivo satisfactoriamente.", strNombreOriginal, strUrl);
            }
            catch (Exception ex)
            {
                return Respuesta(0, ex.Message, strNombreOriginal ?? string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Sobre de respuesta del manejo de archivos. Los textos se serializan:
        /// un nombre de archivo con comillas romperia el JSON si se concatenara.
        /// </summary>
        private static string Respuesta(int intEstado, string strMensaje,
                                        string strOriginal, string strSistema)
        {
            return string.Concat(
                "{\"estado\":", intEstado,
                ",\"mensaje\":", System.Text.Json.JsonSerializer.Serialize(strMensaje),
                ",\"documento_original\":", System.Text.Json.JsonSerializer.Serialize(strOriginal),
                ",\"documento_sistema\":", System.Text.Json.JsonSerializer.Serialize(strSistema), "}");
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