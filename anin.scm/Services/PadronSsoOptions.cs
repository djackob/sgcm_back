namespace anin.scm.Services
{
    /// <summary>
    /// Configuracion del refresco programado del padron del SSO.
    ///
    /// La conexion debe abrir DBSIGCM: el worker no habla con el SSO por su
    /// cuenta, reutiliza SsoAccesoService, que lee el padron de saa_ y se lo
    /// entrega a sigcm.paSincronizarPadronSso.
    /// </summary>
    public sealed class PadronSsoOptions
    {
        public const string Seccion = "PadronSso";

        public bool Habilitado { get; set; }

        /// <summary>
        /// Cada cuanto se reconcilia. Quince minutos es un acuerdo entre dos
        /// costes asimetricos: la corrida son veinte filas y no cuesta nada,
        /// pero cada corrida es una conexion a una base ajena que no es nuestra.
        /// </summary>
        public int IntervaloSegundos { get; set; } = 900;

        /// <summary>
        /// El arranque de la API ya compite con la carga de rutas y la primera
        /// conexion a SQL. La primera corrida espera a que eso pase.
        /// </summary>
        public int EsperaInicialSegundos { get; set; } = 30;
    }
}
