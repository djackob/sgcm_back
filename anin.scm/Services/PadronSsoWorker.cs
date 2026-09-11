using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace anin.scm.Services
{
    /// <summary>
    /// Reconcilia sigcm.Usuario / Unidad / UsuarioRol contra el SSO cada cierto
    /// tiempo, sin que nadie tenga que entrar al sistema.
    ///
    /// QUE PROBLEMA CIERRA
    /// El padron solo se refrescaba al ingresar. Con un token de ocho horas, eso
    /// significa que una jornada entera podia correr contra la foto de la manana:
    /// el correo que cambio en el SSO seguia llegando a la bandeja anterior, y la
    /// persona dada de baja seguia ofreciendose en la lista de derivacion. El
    /// refresco previo a notificar (SsoAccesoService.RefrescarAntesDeNotificar)
    /// tapa el caso del correo, pero solo en el instante del envio. Esto cubre lo
    /// demas: bandejas, derivacion, panel de accesos, altas y bajas de personal.
    ///
    /// POR QUE ADEMAS DEL REFRESCO PREVIO Y NO EN SU LUGAR
    /// Son dos garantias distintas. El refresco previo garantiza que el dato es
    /// exacto en el unico momento en que equivocarse tiene consecuencia externa.
    /// El worker garantiza que el resto del sistema no se aleja mas de un
    /// intervalo de la realidad. Ninguno de los dos sustituye al otro: solo con
    /// el worker quedaria una ventana de quince minutos justo en el envio, y solo
    /// con el refresco previo las listas de derivacion seguirian viejas.
    ///
    /// APAGADO POR DEFECTO. Habilitado se activa en el appsettings del ambiente
    /// que corresponda; una copia de trabajo no tiene por que estar conectandose
    /// sola a la base del SSO.
    ///
    /// El candado lo pone la rutina: sigcm.paSincronizarPadronSso toma un
    /// sp_getapplock de transaccion, de modo que esta corrida y un ingreso
    /// simultaneo no se pisan. Si otra ya estaba en curso, la rutina lo dice y no
    /// reconcilia: aqui no hay nada que reintentar.
    /// </summary>
    public sealed class PadronSsoWorker : BackgroundService
    {
        private readonly PadronSsoOptions _opciones;
        private readonly ILogger<PadronSsoWorker> _logger;

        public PadronSsoWorker(
            IOptions<PadronSsoOptions> opciones,
            ILogger<PadronSsoWorker> logger)
        {
            _opciones = opciones.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opciones.Habilitado)
            {
                _logger.LogInformation(
                    "Refresco programado del padron SSO deshabilitado. El padron solo se "
                    + "pondra al dia al ingresar y al notificar.");
                return;
            }

            _logger.LogInformation(
                "Refresco programado del padron SSO habilitado cada {Intervalo} s.",
                _opciones.IntervaloSegundos);

            if (_opciones.EsperaInicialSegundos > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_opciones.EsperaInicialSegundos), stoppingToken);
            }

            TimeSpan intervalo = TimeSpan.FromSeconds(_opciones.IntervaloSegundos);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Reconciliar();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // El SSO caido no puede tumbar la API. La proxima vuelta lo
                    // reintenta sola; mientras tanto se trabaja con la ultima
                    // reconciliacion, que es la regla de todo el ingreso SSO.
                    _logger.LogError(ex, "Fallo al refrescar el padron del SSO.");
                }

                await Task.Delay(intervalo, stoppingToken);
            }
        }

        /// <summary>
        /// Una corrida. El resumen se registra: una sincronizacion que da de baja
        /// a media entidad, o que descarta a alguien por un cod_perfil sin mapear,
        /// tiene que poder verse sin abrir la base.
        /// </summary>
        private void Reconciliar()
        {
            string strRespuesta = SsoAccesoService.SincronizarPadron(null, "worker");

            JsonNode? jn = null;
            try
            {
                jn = JsonNode.Parse(strRespuesta);
            }
            catch (JsonException)
            {
                _logger.LogWarning(
                    "El refresco del padron devolvio una respuesta que no es JSON: {Respuesta}",
                    strRespuesta);
                return;
            }

            if ((jn?["estado"]?.GetValue<int>() ?? 0) != 1)
            {
                _logger.LogWarning("El refresco del padron no se completo: {Mensaje}",
                    jn?["mensaje"]?.ToString() ?? strRespuesta);
                return;
            }

            JsonNode? jnResumen = jn?["Resumen"];
            JsonNode? jnDescartes = jn?["Descartes"];

            _logger.LogInformation(
                "Padron SSO reconciliado. Resumen: {Resumen}", jnResumen?.ToJsonString() ?? "{}");

            /* Los descartes son gente que no va a poder entrar: un cod_perfil sin
               mapear en sigcm.PerfilSso o un centro de costo sin unidad. Suben a
               Warning porque exigen que alguien haga algo, no solo que lo lea. */
            if (jnDescartes is JsonArray jaDescartes && jaDescartes.Count > 0)
            {
                _logger.LogWarning(
                    "El padron trae {Cantidad} acceso(s) que no se pudieron traducir: {Descartes}",
                    jaDescartes.Count, jaDescartes.ToJsonString());
            }
        }
    }
}
