using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using umg_basic_rover_api.Hubs;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Mqtt;

/// <summary>
/// Servicio MQTT que:
///   1. Publica instrucciones al rover (rover/file/send)
///   2. Publica STOP de emergencia (rover/stop)
///   3. Suscribe a rover/ack y rover/status
///   4. Reenvía esos mensajes al frontend via SignalR (WebSocket)
/// </summary>
public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient            _client;
    private readonly MqttClientOptions      _options;
    private readonly ILogger<MqttService>   _logger;
    private readonly IHubContext<RoverHub>  _hub;

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
        IConfiguration       config,
        ILogger<MqttService> logger,
        IHubContext<RoverHub> hub)
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

        // Registrar handler de mensajes entrantes ANTES de conectar
        _client.ApplicationMessageReceivedAsync += OnMensajeRecibidoAsync;
    }

    private async Task AsegurarConexionAsync()
    {
        if (_client.IsConnected) return;
        try
        {
            await _client.ConnectAsync(_options);
            _logger.LogInformation("[MQTT] Conectado al broker.");

            // Suscribirse a los topics de respuesta del rover
            await _client.SubscribeAsync(TOPIC_ACK,    MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            await _client.SubscribeAsync(TOPIC_STATUS, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            _logger.LogInformation("[MQTT] Suscrito a {ack} y {status}", TOPIC_ACK, TOPIC_STATUS);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al conectar al broker.");
            throw;
        }
    }

    /// <summary>
    /// Handler de mensajes entrantes MQTT.
    /// Reenvía rover/ack y rover/status al frontend via SignalR.
    /// </summary>
    private async Task OnMensajeRecibidoAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic   = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? Array.Empty<byte>());

        _logger.LogDebug("[MQTT→WS] Topic={topic} Payload={payload}", topic, payload);

        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(payload);

            if (topic == TOPIC_ACK)
            {
                // Determinar si es un progreso o un ACK final
                if (data.TryGetProperty("progreso", out _))
                    await _hub.Clients.All.SendAsync("RoverProgreso", data);
                else
                    await _hub.Clients.All.SendAsync("RoverAck", data);
            }
            else if (topic == TOPIC_STATUS)
            {
                await _hub.Clients.All.SendAsync("RoverStatus", data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MQTT→WS] Error procesando mensaje del rover.");
        }
    }

    public async Task<bool> PublicarEjecucionAsync(int compilacion_id, List<RoverInstruccionPayload> instrucciones)
    {
        try
        {
            await AsegurarConexionAsync();

            var payloadObj = new
            {
                compilacion_id,
                instrucciones = instrucciones.Select(i => new InstruccionMqttDto
                {
                    comando = i.comando,
                    @params = i.params_
                }).ToList()
            };

            var json    = JsonSerializer.Serialize(payloadObj, _jsonOpts);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TOPIC_FILE)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogInformation("[MQTT] Publicado en {topic} — id={id}, cmds={n}",
                TOPIC_FILE, compilacion_id, instrucciones.Count);
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
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
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

    private class InstruccionMqttDto
    {
        public string comando { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public Dictionary<string, int> @params { get; set; } = new();
    }
}
