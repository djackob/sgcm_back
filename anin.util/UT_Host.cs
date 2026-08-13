using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace anin.util
{
    public class UT_Host
    {
        public static bool ValidacionHost(string strip)
        {
            bool bsw = false;
            string[] strips = UT_Configuracion.AppSettings("appSettings", "hosts")!.Split(",");
            foreach (var item in strips)
                if (item == strip || item == "::1") { bsw = true; break; }
            ;
            return bsw;
        }
        public static string GetClientIp(HttpContext context)
        {
            string? ip = context.Request.Headers["REMOTE_ADDR"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
            {
                ip = context.Request.Headers["X-Forwarded-For"]
                     .FirstOrDefault()?
                     .Split(',')[0]
                     .Trim();
            }
            if (string.IsNullOrEmpty(ip))
                ip = context.Connection.RemoteIpAddress?.ToString();
            return ip ?? "0.0.0.0";
        }
    }
}
