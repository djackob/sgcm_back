using anin.scm.Services;
using anin.util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace anin.scm.Controllers
{
    [AllowAnonymous]
    public class TokenController : ControllerBase
    {
        [HttpPost]
        public IActionResult tksistema(string strtoken)
        {
            string strResultado = UT_Sso.ValidarAcceso(strtoken);

            StringBuilder sbResultado = new StringBuilder();
            if (strResultado.Equals("{}") || strResultado.Equals(""))
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "ERROR", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat("\"", "Usuario y/o Clave Incorrecta.", "\"}"));
            }
            else
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "OK", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat(TokenService.CreateToken(strResultado.ToString()), "}"));
            }

            return Ok(JsonDocument.Parse(sbResultado.ToString()));
        }

        [HttpPost]
        public IActionResult tksistemaexterno(string strtoken)
        {
            string strResultado = UT_Sso.ValidarAccesoExterno(strtoken);
            StringBuilder sbResultado = new StringBuilder();
            if (strResultado.Equals("{}") || strResultado.Equals(""))
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "ERROR", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat("\"", "Usuario y/o Clave Incorrecta.", "\"}"));
            }
            else
            {
                sbResultado.Append(string.Concat("{\"estado\":\"", "OK", "\",\"mensaje\":"));
                sbResultado.Append(string.Concat(TokenService.CreateToken(strResultado.ToString()), "}"));
            }

            return Ok(JsonDocument.Parse(sbResultado.ToString()));
        }

        [HttpGet]
        public IActionResult LoginOut()
        {
            string strResultado = UT_Sso.LoginOut();
            return Ok(JsonDocument.Parse(strResultado));
        }
    }
}