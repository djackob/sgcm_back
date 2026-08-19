namespace anin.scm.Services
{
    /// <summary>
    /// Configuracion del consumidor de la outbox hacia SIGA.
    ///
    /// La conexion debe abrir DBSIGCM, no SIGA_1750: W001 transforma el JSON
    /// desde la outbox y cruza a SIGA mediante el sinonimo homologado. De este
    /// modo la clave del usuario de SIGA no queda embebida en el worker.
    /// </summary>
    public sealed class IntegracionSigaOptions
    {
        public const string Seccion = "IntegracionSiga";

        public bool Habilitado { get; set; }
        public string Modo { get; set; } = "simulacion";
        public string Conexion { get; set; } = "cnx_sigcm";
        public int IntervaloSegundos { get; set; } = 30;
        public int EsperaInicialSegundos { get; set; } = 5;
        public int Limite { get; set; } = 20;
        public int TimeoutSegundos { get; set; } = 90;
        public string UsuarioAuditoria { get; set; } = "sigcm-worker";
    }
}
