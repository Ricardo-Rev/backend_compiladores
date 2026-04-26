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
/// CORRECCIÓN: El payload ya no usa {comando, params} que la Raspberry
/// tenía que interpretar. Ahora publica strings seriales directos:
///   ["GR:-1", "AV_CM:30"] — la Raspberry los escribe tal cual al serial.
/// Esto elimina cualquier bug de conversión en el script de la Raspberry.
/// </summary>
public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient          _client;
    private readonly MqttClientOptions    _options;
    private readonly ILogger<MqttService> _logger;
    private readonly IRoverHubNotifier    _hub;

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

        _options = new MqttClientOptionsBuilder()
            .WithClientId($"umg-backend-{Guid.NewGuid():N}")
            .WithTcpServer(broker, port)
            .WithCredentials(user, password)
            .WithTlsOptions(o => o.UseTls())
            .WithCleanSession()
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMensajeRecibidoAsync;
    }

    private async Task AsegurarConexionAsync()
    {
        if (_client.IsConnected) return;

        try
        {
            await _client.ConnectAsync(_options);
            _logger.LogInformation("[MQTT] Conectado al broker.");

            await _client.SubscribeAsync(TOPIC_ACK,
                MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            await _client.SubscribeAsync(TOPIC_STATUS,
                MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            _logger.LogInformation("[MQTT] Suscrito a {ack} y {status}", TOPIC_ACK, TOPIC_STATUS);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al conectar al broker.");
            throw;
        }
    }

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
            _logger.LogInformation("[MQTT] Publicado en {topic} — id={id}, cmds={n}: [{cmds}]",
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
    }
}