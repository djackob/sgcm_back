using anin.util;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;


namespace anin.dataAccess
{
    public class DaProceso
    {
        public string ejecutarProceso(string strConexion, string strNombreProcesoFuncion, string strParametro = "", int intTimeOut = 30)
        {
            string strResultado = string.Empty;
            try
            {
                using (NpgsqlConnection ncConexion = new NpgsqlConnection(UT_Configuracion.AppSettings("ConnectionStrings", strConexion)))
                {
                    ncConexion.Open();
                    NpgsqlCommand ncmComando = new NpgsqlCommand();
                    ncmComando.Connection = ncConexion;
                    ncmComando.CommandType = CommandType.Text;
                    ncmComando.CommandTimeout = intTimeOut;
                    if (!string.IsNullOrEmpty(strParametro))
                    {
                        strParametro = strParametro.Replace("'", "''");
                        ncmComando.CommandText = string.Format("select {0}('{1}');", strNombreProcesoFuncion, strParametro);
                    }
                    else
                    {
                        ncmComando.CommandText = string.Format("select {0}();", strNombreProcesoFuncion);
                    }

                    ncmComando.Parameters.Clear(); 
                    NpgsqlDataReader drData = ncmComando.ExecuteReader();
                    if (drData.HasRows)
                    {
                        if (drData.Read())
                        {
                            if (!drData.IsDBNull(0))
                            {
                                strResultado = drData.GetString(0);
                            }
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                if (!string.IsNullOrEmpty(ex.SqlState) && ex.SqlState.Equals("P0001"))
                {
                    strResultado = "{\"resultado\":-2}";
                }
                else
                {
                    //strResultado = "{\"resultado\":-1}";
                    strResultado = "{\"resultado\":\"" + ex.Message  + "\"}";
                }
            }

            return strResultado;
        }

    }
}
