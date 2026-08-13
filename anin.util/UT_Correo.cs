using System.Net.Mail;
using System.Net.Security;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace anin.util
{
    public class UT_Correo
    {
        public static string envioCorreo(string strDe, string strPara, string strAsunto,
            string strMensaje, string? strParaCopia = null)
        {
            string StrEstado = "";
            try
            {
                string? strDestinatario = UT_Configuracion.AppSettings("appSettings:app_correo", strDe);
                MailMessage msg = new MailMessage();
                msg.From = new MailAddress(strDestinatario);

                string[] strLista = strPara.Split(';');
                foreach (string strItem in strLista)
                {
                    if (!string.IsNullOrEmpty(strItem))
                    {
                        msg.To.Add(new MailAddress(strItem));
                    }
                }

                if (!string.IsNullOrEmpty(strParaCopia))
                {
                    string[] strListaCopia = strParaCopia.Split(';');
                    foreach (string strItem in strListaCopia)
                    {
                        msg.CC.Add(new MailAddress(strItem));
                    }
                }

                msg.IsBodyHtml = true;
                msg.SubjectEncoding = System.Text.Encoding.UTF8;
                msg.Subject =  strAsunto;
                msg.BodyEncoding = System.Text.Encoding.UTF8;
                msg.Body = strMensaje;
                msg.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Host = UT_Configuracion.AppSettings("appSettings:app_correo", "host");
                smtpClient.Port = int.Parse(UT_Configuracion.AppSettings("appSettings:app_correo", "puerto"));
                smtpClient.EnableSsl = true;
                NetworkCredential credentials = new NetworkCredential(UT_Configuracion.AppSettings("appSettings:app_correo", "de"), UT_Configuracion.AppSettings("appSettings:app_correo", "clave"));
                smtpClient.Credentials = credentials;
#pragma warning disable CS8622 // La nulabilidad de los tipos de referencia del tipo de parámetro no coincide con el delegado de destino (posiblemente debido a los atributos de nulabilidad).
                ServicePointManager.ServerCertificateValidationCallback = delegate (object s,
                    X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                { return true; };
#pragma warning restore CS8622 // La nulabilidad de los tipos de referencia del tipo de parámetro no coincide con el delegado de destino (posiblemente debido a los atributos de nulabilidad).
                smtpClient.Send(msg);
                StrEstado = "{\"estado\":1,\"mensaje\":\"Envío de correo satisfactorio\"}";
            }
            catch (Exception ex)
            {
                StrEstado = "{\"estado\":0,\"mensaje\":\"" + ex.Message + "\"}";
            }
            return StrEstado;
        }
    }
}
