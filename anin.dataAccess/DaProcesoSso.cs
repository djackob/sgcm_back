using anin.util;
using Npgsql;
using System.Text.Json;

namespace anin.dataAccess
{
    /// <summary>
    /// Lector del padron del SSO. Es la unica pieza del backend que habla con
    /// PostgreSQL, y habla SOLO PARA LEER.
    ///
    /// QUE TRAE Y POR QUE HACE FALTA
    /// El servicio de token del SSO (url_token) certifica la identidad, pero su
    /// respuesta no incluye el centro de costo ni el codigo de perfil, que es
    /// justamente lo que el SIGCM necesita para ubicar a la persona en una
    /// unidad y en un rol. Eso vive en la base del SSO, en
    /// login.fn_listar_login_usuario_perfil_sistema_sgcm, que devuelve el padron
    /// filtrado al sistema 73 (SGCM-I).
    ///
    /// AQUI NO SE INTERPRETA NADA
    /// El JSON que devuelve la funcion se entrega TAL CUAL a
    /// sigcm.paSincronizarPadronSso, que es donde se traduce el cod_perfil a un
    /// rol del SIGCM y se reconcilia el padron. Toda la logica de negocio vive
    /// en la base (ESTANDARES.md, seccion 1) y la traduccion de perfiles es
    /// logica de negocio: quien puede entrar y con que autoridad.
    ///
    /// Deserializar aqui el padron para volver a serializarlo habria sido, ademas
    /// de trabajo perdido, una segunda definicion de la forma del dato que se
    /// desincroniza en cuanto el SSO agregue una columna.
    /// </summary>
    public class DaProcesoSso
    {
        /// <summary>Clave dentro de ConnectionStrings de appsettings.json.</summary>
        public const string CONEXION = "cnx_saa_";

        /// <summary>
        /// El padron completo de accesos vigentes al SGCM, como lo devuelve el
        /// SSO: { "usuario":[ ... ], "cantidad": n }.
        ///
        /// Se pide SIN FILTRO a proposito. La reconciliacion de la base da de
        /// baja por diferencia de conjuntos, y para eso necesita el conjunto
        /// entero: el SSO no marca como inactivo a quien dio de baja, lo deja
        /// fuera del resultado. Un padron filtrado por una sola cuenta haria que
        /// la sincronizacion leyera "ya no trabaja nadie mas aqui". La rutina se
        /// protege igual (salvaguarda 1 de F008), pero el filtro correcto es no
        /// poner ninguno.
        /// </summary>
        public string ObtenerPadron(int intTimeOut = 30)
        {
            return EjecutarEscalar(
                "SELECT login.fn_listar_login_usuario_perfil_sistema_sgcm(NULL)::text;",
                intTimeOut);
        }

        /// <summary>
        /// Las dependencias con centro de costo, que son las unidades organicas
        /// tal como el SSO las conoce, incluido su arbol (id_padre).
        ///
        /// Va aparte del padron porque la funcion del SSO devuelve la dependencia
        /// del usuario ya concatenada -"UDS; UA" cuando son dos- y sin el codigo
        /// ni la jerarquia. Con la tabla, sigcm.Unidad se puebla completa: sigla,
        /// nombre, centro de costo y unidad padre.
        ///
        /// Sin esto, una instalacion limpia no tendria ninguna unidad -hoy solo
        /// las crea S900, que es semilla de pruebas- y ninguna terna del SSO
        /// podria aterrizar.
        /// </summary>
        public string ObtenerDependencia(int intTimeOut = 30)
        {
            return EjecutarEscalar(@"
                SELECT COALESCE(json_agg(d)::text, '[]')
                  FROM (
                        SELECT id_dependencia, id_padre, cod_dependencia,
                               siglas, descripcion, centro_costo
                          FROM login.tm_login_dependencia
                         WHERE COALESCE(activo, true)
                           AND centro_costo IS NOT NULL
                           AND btrim(centro_costo) <> ''
                         ORDER BY id_dependencia
                       ) AS d;", intTimeOut);
        }

        /// <summary>
        /// El sobre completo que espera sigcm.paSincronizarPadronSso, armado con
        /// las dos lecturas. Devuelve null si cualquiera de las dos falla: media
        /// sincronizacion es peor que ninguna, porque el padron incompleto es
        /// exactamente lo que la reconciliacion interpreta como bajas.
        /// </summary>
        public string? ArmarSobreSincronizacion(string strDisparador, string? strCuenta,
                                                string? strEquipo, string? strPrograma)
        {
            string strPadron = ObtenerPadron();
            string strDependencia = ObtenerDependencia();

            if (string.IsNullOrWhiteSpace(strPadron) || strPadron.StartsWith("{\"estado\":0"))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(strDependencia) || strDependencia.StartsWith("{\"estado\":0"))
            {
                return null;
            }

            return string.Concat(
                "{\"Disparador\":", JsonSerializer.Serialize(strDisparador),
                ",\"Cuenta\":", JsonSerializer.Serialize(strCuenta ?? string.Empty),
                ",\"Completo\":true",
                ",\"Equipo\":", JsonSerializer.Serialize(strEquipo ?? "sso"),
                ",\"Programa\":", JsonSerializer.Serialize(strPrograma ?? "SIGCM-SSO"),
                ",\"Dependencia\":", strDependencia,
                ",\"Padron\":", strPadron,
                "}");
        }

        private static string EjecutarEscalar(string strSentencia, int intTimeOut)
        {
            try
            {
                string? strCadena = UT_Configuracion.AppSettings("ConnectionStrings", CONEXION);

                if (string.IsNullOrEmpty(strCadena))
                {
                    return ErrorInfraestructura(
                        "No existe la cadena de conexion '" + CONEXION + "' en appsettings.json.");
                }

                using (NpgsqlConnection ncConexion = new NpgsqlConnection(strCadena))
                {
                    ncConexion.Open();

                    using (NpgsqlCommand ncmComando = new NpgsqlCommand(strSentencia, ncConexion))
                    {
                        ncmComando.CommandTimeout = intTimeOut;

                        // ExecuteScalar basta: la consulta devuelve una sola
                        // celda de texto y Npgsql no la trunca.
                        object? objResultado = ncmComando.ExecuteScalar();

                        return objResultado == null || objResultado == DBNull.Value
                            ? string.Empty
                            : objResultado.ToString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                // Mismo sobre de error que emite DaProceso, para que el llamador
                // tenga una sola forma de respuesta que interpretar venga el
                // fallo de SQL Server o de PostgreSQL.
                return ErrorInfraestructura(ex.Message);
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
