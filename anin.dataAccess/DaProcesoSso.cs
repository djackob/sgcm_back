using anin.util;
using Npgsql;
using System.Text.Json;

namespace anin.dataAccess
{
    /// <summary>
    /// Ejecutor de sentencias contra la base PostgreSQL del SSO (saa_).
    /// Recibe la sentencia, opcionalmente un JSON como $1, y devuelve el JSON
    /// de la unica celda. No arma SQL de negocio ni interpreta la respuesta.
    /// </summary>
    public class DaProcesoSso
    {
        /// <summary>Clave dentro de ConnectionStrings de appsettings.json.</summary>
        public const string CONEXION = "cnx_saa_";

        public JsonDocument EjecutarProceso(string strSentencia, int intTimeOut = 30,
                                            string? strParametroJson = null)
        {
            string strPayload;

            try
            {
                string? strCadena = UT_Configuracion.AppSettings("ConnectionStrings", CONEXION);

                if (string.IsNullOrEmpty(strCadena))
                {
                    strPayload = ErrorInfraestructura(
                        "No existe la cadena de conexion '" + CONEXION + "' en appsettings.json.");
                }
                else
                {
                    using (NpgsqlConnection ncConexion = new NpgsqlConnection(strCadena))
                    {
                        ncConexion.Open();

                        using (NpgsqlCommand ncmComando = new NpgsqlCommand(strSentencia, ncConexion))
                        {
                            ncmComando.CommandTimeout = intTimeOut;
                            if (strParametroJson != null)
                            {
                                /* Las sentencias usan $1 (posicional). Un parametro
                                   nombrado p1 no se enlaza y Postgres recibe 0
                                   argumentos: ListarProvincia/Distrito devolvian
                                   error y el combo quedaba vacio. */
                                ncmComando.Parameters.Add(new NpgsqlParameter
                                {
                                    NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Json,
                                    Value = strParametroJson
                                });
                            }

                            object? objResultado = ncmComando.ExecuteScalar();

                            strPayload = objResultado == null || objResultado == DBNull.Value
                                ? string.Empty
                                : objResultado.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                strPayload = ErrorInfraestructura(ex.Message);
            }

            if (string.IsNullOrWhiteSpace(strPayload))
            {
                return JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"INFRAESTRUCTURA\",\"mensaje\":\"La funcion no devolvio datos.\"}");
            }

            try
            {
                return JsonDocument.Parse(strPayload);
            }
            catch (JsonException)
            {
                return JsonDocument.Parse(
                    "{\"estado\":0,\"codigo\":\"CONTRATO\",\"mensaje\":\"La funcion devolvio una respuesta que no es JSON.\"}");
            }
        }

        private static string ErrorInfraestructura(string strMensaje)
        {
            return string.Concat(
                "{\"estado\":0,\"codigo\":\"INFRAESTRUCTURA\",\"mensaje\":",
                JsonSerializer.Serialize(strMensaje), "}");
        }
    }
}
