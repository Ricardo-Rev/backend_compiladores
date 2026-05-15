using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Mqtt;

/// <summary>
/// Servicio MQTT que:
///   1. Publica comandos seriales al rover  (rover/file/send)
///   2. Publica STOP de emergencia          (rover/stop)
///   3. Suscribe a rover/ack y rover/status
///   4. Reenvía esos mensajes al frontend via IRoverHubNotifier → SignalR
///
/// CORRECCIONES:
///   [BUG-1] SemaphoreSlim en AsegurarConexionAsync: evita que dos requests
///           concurrentes llamen ConnectAsync al mismo tiempo cuando el cliente
///           está desconectado (race condition → excepción o suscripciones duplicadas).
///   [BUG-2] Handler DisconnectedAsync: reconecta y re-suscribe automáticamente
///           cuando el broker cae, para no perder ACKs del rover durante ejecución.
/// </summary>
public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient          _client;
    private readonly MqttClientOptions    _options;
    private readonly ILogger<MqttService> _logger;
    private readonly IRoverHubNotifier    _hub;

    // [BUG-1] Semáforo que garantiza que solo un hilo conecta a la vez
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private const string TOPIC_FILE   = "rover/file/send";
    private const string TOPIC_STOP   = "rover/stop";
    private const string TOPIC_ACK    = "rover/ack";
    private const string TOPIC_STATUS = "rover/status";

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented          = false
    };

    public bool EstaConectado => _client.IsConnected;

    public MqttService(
        IConfiguration        config,
        ILogger<MqttService>  logger,
        IRoverHubNotifier     hub)
    {
        _logger = logger;
        _hub    = hub;

        var broker   = config["Mqtt:Broker"]   ?? throw new InvalidOperationException("Mqtt:Broker no configurado.");
        var port     = int.Parse(config["Mqtt:Port"] ?? "8883");
        var user     = config["Mqtt:User"]     ?? throw new InvalidOperationException("Mqtt:User no configurado.");
        var password = config["Mqtt:Password"] ?? throw new InvalidOperationException("Mqtt:Password no configurado.");

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        // Lee MQTT_CLIENT_ID desde variable de entorno (Railway).
        // Un client_id fijo evita que el broker expulse sesiones anteriores
        // con rc=7 cuando el backend reconecta o Railway reinicia el contenedor.
        var clientId = Environment.GetEnvironmentVariable("MQTT_CLIENT_ID")
                       ?? config["Mqtt:ClientId"]
                       ?? "umg-backend-prod";

        _options = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(broker, port)
            .WithCredentials(user, password)
            .WithTlsOptions(o => o.UseTls())
            .WithCleanSession()
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMensajeRecibidoAsync;

        // [BUG-2] Reconectar y re-suscribir cuando el broker cae o la red falla
        _client.DisconnectedAsync += OnDesconectadoAsync;
    }

    // ── [BUG-2] Handler de desconexión ───────────────────────────────────────
    private async Task OnDesconectadoAsync(MqttClientDisconnectedEventArgs e)
    {
        if (e.Reason == MqttClientDisconnectReason.NormalDisconnection)
            return; // desconexión voluntaria (DisposeAsync) — no reconectar

        _logger.LogWarning("[MQTT] Conexión perdida (razón: {r}). Reconectando en 5 s...", e.Reason);

        int espera = 5;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(espera));
            try
            {
                await _client.ConnectAsync(_options);
                await SuscribirAsync();
                _logger.LogInformation("[MQTT] Reconectado al broker.");
                return;
            }
            catch (Exception ex)
            {
                espera = Math.Min(60, espera * 2); // backoff exponencial hasta 60 s
                _logger.LogWarning(ex, "[MQTT] Reconexión fallida. Reintentando en {s} s...", espera);
            }
        }
    }

    // ── Suscripciones ─────────────────────────────────────────────────────────
    private async Task SuscribirAsync()
    {
        await _client.SubscribeAsync(TOPIC_ACK,
            MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        await _client.SubscribeAsync(TOPIC_STATUS,
            MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        _logger.LogInformation("[MQTT] Suscrito a {ack} y {status}", TOPIC_ACK, TOPIC_STATUS);
    }

    // ── [BUG-1] Conexión thread-safe con doble verificación ──────────────────
    private async Task AsegurarConexionAsync()
    {
        if (_client.IsConnected) return;

        await _connectLock.WaitAsync();
        try
        {
            // Segunda comprobación dentro del lock (patrón double-check)
            if (_client.IsConnected) return;

            _logger.LogInformation("[MQTT] Conectando al broker...");
            await _client.ConnectAsync(_options);
            await SuscribirAsync();
            _logger.LogInformation("[MQTT] Conectado al broker.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al conectar al broker.");
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    // ── Mensajes entrantes → SignalR ─────────────────────────────────────────
    private async Task OnMensajeRecibidoAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic   = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(
            e.ApplicationMessage.PayloadSegment.ToArray());

        _logger.LogDebug("[MQTT→WS] {topic}: {payload}", topic, payload);

        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(payload);

            if (topic == TOPIC_ACK)
            {
                if (data.TryGetProperty("progreso", out _))
                    await _hub.NotificarProgresoAsync(data);
                else
                    await _hub.NotificarAckAsync(data);
            }
            else if (topic == TOPIC_STATUS)
            {
                await _hub.NotificarStatusAsync(data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MQTT→WS] Error procesando mensaje entrante.");
        }
    }

    /// <summary>
    /// Publica comandos seriales directos al rover.
    /// Formato del JSON publicado en rover/file/send:
    /// {
    ///   "compilacion_id": 5,
    ///   "comandos": ["GR:-1", "AV_CM:30", "GR:1"]
    /// }
    /// La Raspberry lee "comandos" y escribe cada uno al serial del Arduino
    /// seguido de '\n', sin ninguna transformación adicional.
    /// </summary>
    public async Task<bool> PublicarEjecucionAsync(
        int compilacion_id,
        List<string> comandos_serial)
    {
        try
        {
            await AsegurarConexionAsync();

            var payloadObj = new
            {
                compilacion_id,
                comandos = comandos_serial
            };

            var json    = JsonSerializer.Serialize(payloadObj, _jsonOpts);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TOPIC_FILE)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(
                    MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogInformation(
                "[MQTT] Publicado en {topic} — id={id}, cmds={n}: [{cmds}]",
                TOPIC_FILE, compilacion_id, comandos_serial.Count,
                string.Join(", ", comandos_serial));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al publicar ejecución.");
            return false;
        }
    }

    public async Task<bool> PublicarStopAsync()
    {
        try
        {
            await AsegurarConexionAsync();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TOPIC_STOP)
                .WithPayload(Encoding.UTF8.GetBytes("{\"command\":\"STOP\"}"))
                .WithQualityOfServiceLevel(
                    MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogInformation("[MQTT] STOP publicado.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al publicar STOP.");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
        _connectLock.Dispose();
    }
}