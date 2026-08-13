using anin.util;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace anin.dataAccess
{
    /// <summary>
    /// Ejecutor unico de rutinas de base de datos. Es la totalidad del acceso a
    /// datos del sistema: aqui no se arma SQL de negocio, no se mapean entidades
    /// y no se decide nada. El backend es un puente.
    ///
    /// CONTRATO (ver SIGCM_SERVER/db/README.md y Proyecto/ESTANDARES.md)
    /// Una rutina invocable recibe UN parametro nvarchar(max) con JSON y
    /// devuelve UNA fila con UNA columna de texto, tambien JSON, que este metodo
    /// entrega tal cual al controlador.
    ///
    /// CAMBIO FRENTE A LA VERSION POSTGRESQL
    /// El motor es SQL Server y las rutinas son PROCEDIMIENTOS, no funciones:
    /// SQL Server prohibe DML dentro de una funcion. Por eso se invoca con
    /// CommandType.StoredProcedure y no concatenando "select fn(...)".
    ///
    /// El JSON viaja como PARAMETRO, nunca concatenado en el texto del comando.
    /// La version PostgreSQL lo interpolaba duplicando comillas simples; eso
    /// convertia cada sustento escrito por un usuario en una posible inyeccion y
    /// ademas rompia los payloads con apostrofes. Con SqlParameter el problema
    /// desaparece de raiz.
    ///
    /// El mapeo de SqlState P0001 que hacia la version anterior NO se replica:
    /// los errores de negocio ya no viajan como excepcion, llegan como payload
    /// con estado 0 desde el CATCH del procedimiento. Lo que se atrapa aqui son
    /// solo fallos de infraestructura: conexion caida, tiempo agotado, permiso
    /// denegado.
    /// </summary>
    public class DaProceso
    {
        /// <param name="strConexion">Clave dentro de ConnectionStrings de appsettings.json.</param>
        /// <param name="strNombreProcesoFuncion">Procedimiento con esquema: cmn.paRegistrarSolicitud.</param>
        /// <param name="strParametro">JSON del sobre de entrada. Vacio si la rutina no lo pide.</param>
        /// <param name="intTimeOut">Segundos. El de la conexion manda si es menor.</param>
        public string ejecutarProceso(string strConexion, string strNombreProcesoFuncion,
                                      string strParametro = "", int intTimeOut = 30)
        {
            string strResultado = string.Empty;

            try
            {
                string? strCadena = UT_Configuracion.AppSettings("ConnectionStrings", strConexion);

                if (string.IsNullOrEmpty(strCadena))
                {
                    return ErrorInfraestructura(
                        string.Concat("No existe la cadena de conexion '", strConexion, "' en appsettings.json."));
                }

                using (SqlConnection scConexion = new SqlConnection(strCadena))
                {
                    scConexion.Open();

                    using (SqlCommand scmComando = new SqlCommand(strNombreProcesoFuncion, scConexion))
                    {
                        scmComando.CommandType = CommandType.StoredProcedure;
                        scmComando.CommandTimeout = intTimeOut;

                        // Las rutinas sin sobre de entrada declaran @parametro con
                        // valor por defecto; enviar cadena vacia seria un JSON
                        // invalido y las haria fallar por su propia validacion.
                        if (!string.IsNullOrEmpty(strParametro))
                        {
                            scmComando.Parameters.Add("@parametro", SqlDbType.NVarChar, -1).Value = strParametro;
                        }

                        // ExecuteReader y GetString en vez de ExecuteScalar: el
                        // payload de una bandeja o de un Anexo 3 completo supera
                        // con holgura los limites en los que ExecuteScalar puede
                        // truncar segun el proveedor.
                        using (SqlDataReader drData = scmComando.ExecuteReader())
                        {
                            if (drData.Read() && !drData.IsDBNull(0))
                            {
                                strResultado = drData.GetString(0);
                            }
                        }
                    }
                }

                // Una rutina que no devuelve fila incumple el contrato. Se
                // reporta como tal en vez de propagar un JSON vacio que el
                // frontend interpretaria como respuesta valida.
                if (string.IsNullOrEmpty(strResultado))
                {
                    return ErrorInfraestructura(
                        string.Concat("La rutina ", strNombreProcesoFuncion,
                                      " no devolvio ninguna fila. Revise el contrato de la rutina."));
                }
            }
            catch (SqlException ex)
            {
                strResultado = ErrorInfraestructura(ex.Message);
            }
            catch (Exception ex)
            {
                strResultado = ErrorInfraestructura(ex.Message);
            }

            return strResultado;
        }

        /// <summary>
        /// Devuelve el mismo sobre de error que emite el CATCH de las rutinas,
        /// para que el frontend tenga una sola forma de respuesta que interpretar
        /// venga el fallo de la base o de la conexion.
        /// El mensaje se serializa: un texto de excepcion con comillas romperia
        /// el JSON si se concatenara a mano.
        /// </summary>
        private static string ErrorInfraestructura(string strMensaje)
        {
            return string.Concat(
                "{\"estado\":0,\"codigo\":\"INFRAESTRUCTURA\",\"mensaje\":",
                JsonSerializer.Serialize(strMensaje), "}");
        }
    }
}
