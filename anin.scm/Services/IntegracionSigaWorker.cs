using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text.Json;

namespace anin.scm.Services
{
    /// <summary>
    /// Consume periodicamente integracion.Operacion mediante W001, W002 y W003.
    ///
    /// El worker no reimplementa la escritura ni abre SIGA directamente. Toda
    /// la traduccion, validacion, idempotencia, reintentos y mapeo permanecen en
    /// integracion.paEscribirCuadroModificado, que es la frontera transaccional.
    ///
    /// sp_getapplock evita que dos replicas de la API drenen el mismo lote al
    /// mismo tiempo. El bloqueo pertenece a la sesion y se libera explicitamente
    /// antes de devolver la conexion al pool.
    /// </summary>
    public sealed class IntegracionSigaWorker : BackgroundService
    {
        private const string RecursoBloqueo = "SIGCM:IntegracionSigaWorker";

        private readonly IntegracionSigaOptions _opciones;
        private readonly ILogger<IntegracionSigaWorker> _logger;
        private readonly string? _cadenaConexion;

        public IntegracionSigaWorker(
            IOptions<IntegracionSigaOptions> opciones,
            IConfiguration configuration,
            ILogger<IntegracionSigaWorker> logger)
        {
            _opciones = opciones.Value;
            _logger = logger;
            _cadenaConexion = configuration.GetConnectionString(_opciones.Conexion);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opciones.Habilitado)
            {
                _logger.LogInformation("Worker de integracion SIGA deshabilitado.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_cadenaConexion))
            {
                throw new InvalidOperationException(
                    $"No existe ConnectionStrings:{_opciones.Conexion} para el worker de integracion SIGA.");
            }

            string modo = _opciones.Modo.ToLowerInvariant();
            if (modo == "real")
            {
                _logger.LogWarning(
                    "Worker de integracion SIGA habilitado en MODO REAL. Conexion: {Conexion}.",
                    _opciones.Conexion);
            }
            else
            {
                _logger.LogInformation("Worker de integracion SIGA habilitado en simulacion.");
            }

            if (_opciones.EsperaInicialSegundos > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_opciones.EsperaInicialSegundos), stoppingToken);
            }

            TimeSpan intervalo = TimeSpan.FromSeconds(_opciones.IntervaloSegundos);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcesarLoteAsync(modo, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Fallo de infraestructura al ejecutar el worker de integracion SIGA.");
                }

                await Task.Delay(intervalo, stoppingToken);
            }
        }

        private async Task ProcesarLoteAsync(string modo, CancellationToken cancellationToken)
        {
            await using var conexion = new SqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            bool bloqueoTomado = await TomarBloqueoAsync(conexion, cancellationToken);
            if (!bloqueoTomado)
            {
                _logger.LogDebug("Otra instancia esta procesando la cola SIGA; se omite este ciclo.");
                return;
            }

            try
            {
                await DrenarEscritorAsync(conexion, "integracion.paEscribirCuadroModificado", modo, cancellationToken);
                await DrenarEscritorAsync(conexion, "integracion.paEscribirCuadroAdquisicion", modo, cancellationToken);
                await DrenarEscritorAsync(conexion, "integracion.paEscribirOrdenServicio", modo, cancellationToken);
                await DrenarEscritorAsync(conexion, "integracion.paEscribirRecepcionOrden", modo, cancellationToken);
            }
            finally
            {
                await LiberarBloqueoAsync(conexion);
            }
        }

        private async Task DrenarEscritorAsync(
            SqlConnection conexion, string procedimiento, string modo, CancellationToken cancellationToken)
        {
            string parametro = JsonSerializer.Serialize(new
            {
                Actor = new
                {
                    Usuario = _opciones.UsuarioAuditoria,
                    Equipo = Environment.MachineName,
                    Programa = "SIGCM-WORKER"
                },
                Modo = modo,
                Limite = _opciones.Limite
            });

            await using var comando = new SqlCommand(procedimiento, conexion)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _opciones.TimeoutSegundos
            };
            comando.Parameters.Add("@parametro", SqlDbType.NVarChar, -1).Value = parametro;

            string respuesta = await LeerRespuestaJsonAsync(comando, cancellationToken);
            RegistrarResultado(respuesta, modo, procedimiento);
        }

        private static async Task<bool> TomarBloqueoAsync(
            SqlConnection conexion, CancellationToken cancellationToken)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = """
                DECLARE @resultado int;
                EXEC @resultado = sys.sp_getapplock
                     @Resource = @recurso,
                     @LockMode = 'Exclusive',
                     @LockOwner = 'Session',
                     @LockTimeout = 0;
                SELECT @resultado;
                """;
            comando.Parameters.Add("@recurso", SqlDbType.NVarChar, 255).Value = RecursoBloqueo;

            object? resultado = await comando.ExecuteScalarAsync(cancellationToken);
            return resultado != null && resultado != DBNull.Value && Convert.ToInt32(resultado) >= 0;
        }

        private static async Task LiberarBloqueoAsync(SqlConnection conexion)
        {
            if (conexion.State != ConnectionState.Open)
            {
                return;
            }

            try
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText = """
                    EXEC sys.sp_releaseapplock
                         @Resource = @recurso,
                         @LockOwner = 'Session';
                    """;
                comando.Parameters.Add("@recurso", SqlDbType.NVarChar, 255).Value = RecursoBloqueo;
                await comando.ExecuteNonQueryAsync();
            }
            catch
            {
                // Al cerrarse la conexion SQL Server libera los bloqueos de
                // sesion. No se oculta el error principal por fallar el release.
            }
        }

        private static async Task<string> LeerRespuestaJsonAsync(
            SqlCommand comando, CancellationToken cancellationToken)
        {
            await using SqlDataReader lector = await comando.ExecuteReaderAsync(cancellationToken);

            do
            {
                // El procedimiento de SIGA puede emitir antes su propio result
                // set. El contrato de W001 es la columna final "respuesta".
                if (lector.FieldCount == 1
                    && lector.GetName(0).Equals("respuesta", StringComparison.OrdinalIgnoreCase)
                    && await lector.ReadAsync(cancellationToken)
                    && !lector.IsDBNull(0))
                {
                    return lector.GetString(0);
                }
            }
            while (await lector.NextResultAsync(cancellationToken));

            throw new InvalidOperationException(
                "integracion.paEscribirCuadroModificado no devolvio la columna respuesta.");
        }

        private void RegistrarResultado(string respuesta, string modo, string procedimiento)
        {
            using JsonDocument documento = JsonDocument.Parse(respuesta);
            JsonElement raiz = documento.RootElement;

            int estado = raiz.TryGetProperty("estado", out JsonElement valorEstado)
                && valorEstado.TryGetInt32(out int estadoLeido)
                    ? estadoLeido : 0;

            if (estado != 1)
            {
                string mensaje = raiz.TryGetProperty("mensaje", out JsonElement valorMensaje)
                    ? valorMensaje.ToString() : "Respuesta sin mensaje.";
                _logger.LogError("El drenaje SIGA ({Procedimiento}) fue rechazado: {Mensaje}", procedimiento, mensaje);
                return;
            }

            int tomadas = LeerEntero(raiz, "Tomadas");
            int escritas = LeerEntero(raiz, "Escritas");
            int simuladas = LeerEntero(raiz, "Simuladas");
            int conError = LeerEntero(raiz, "ConError");

            if (conError > 0
                && raiz.TryGetProperty("Detalle", out JsonElement detalle)
                && detalle.ValueKind == JsonValueKind.Array)
            {
                string[] errores = detalle.EnumerateArray()
                    .Where(item => item.TryGetProperty("Resultado", out JsonElement resultado)
                                   && resultado.GetString() == "ERROR")
                    .Select(item => item.TryGetProperty("Mensaje", out JsonElement mensaje)
                        ? mensaje.ToString() : "Error sin detalle.")
                    .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                    .Distinct()
                    .Take(3)
                    .ToArray();

                if (errores.Length > 0)
                {
                    _logger.LogWarning("SIGA rechazo operaciones: {Errores}",
                        string.Join(" | ", errores));
                }
            }

            if (tomadas > 0 || conError > 0)
            {
                _logger.LogInformation(
                    "Drenaje SIGA ({Procedimiento}) terminado. Modo={Modo}, Tomadas={Tomadas}, Escritas={Escritas}, Simuladas={Simuladas}, ConError={ConError}.",
                    procedimiento, modo, tomadas, escritas, simuladas, conError);
            }
            else
            {
                _logger.LogDebug("Cola SIGA sin operaciones listas para procesar.");
            }
        }

        private static int LeerEntero(JsonElement raiz, string nombre)
        {
            return raiz.TryGetProperty(nombre, out JsonElement valor)
                   && valor.TryGetInt32(out int entero)
                ? entero : 0;
        }
    }
}
