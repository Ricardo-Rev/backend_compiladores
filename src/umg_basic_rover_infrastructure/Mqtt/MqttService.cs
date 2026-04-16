using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Mqtt;

public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient         _client;
    private readonly MqttClientOptions   _options;
    private readonly ILogger<MqttService> _logger;

    private const string TOPIC_FILE = "rover/file/send";
    private const string TOPIC_STOP = "rover/stop";

    // Opciones de serialización: snake_case y sin valores nulos
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    public bool EstaConectado => _client.IsConnected;

    public MqttService(IConfiguration config, ILogger<MqttService> logger)
    {
        _logger = logger;

        // CORRECCIÓN: las claves deben coincidir con la sección "Mqtt" del appsettings.json
        // En Railway se configuran como variables de entorno:
        //   Mqtt__Broker, Mqtt__Port, Mqtt__User, Mqtt__Password
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
    }

    private async Task AsegurarConexionAsync()
    {
        if (_client.IsConnected) return;

        try
        {
            await _client.ConnectAsync(_options);
            _logger.LogInformation("[MQTT] Conectado al broker.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT] Error al conectar al broker.");
            throw;
        }
    }

    public async Task<bool> PublicarEjecucionAsync(int compilacion_id, List<RoverInstruccionPayload> instrucciones)
    {
        try
        {
            await AsegurarConexionAsync();

            // CORRECCIÓN: el payload debe tener exactamente las claves que espera el agente Python:
            //   { "compilacion_id": 42, "instrucciones": [ {"comando": "...", "params": {...} } ] }
            //
            // NOTA: el agente Python lee inst.get("params", {}) — NO "params_"
            // La serialización con SnakeCaseLower convierte "params_" → "params_" (no cambia el guión bajo)
            // Por eso usamos un DTO anónimo con la clave exacta "params".
            var payloadObj = new
            {
                compilacion_id,
                instrucciones = instrucciones.Select(i => new InstruccionMqttDto
                {
                    comando = i.comando,
                    @params = i.params_      // @params serializa como "params" en JSON
                }).ToList()
            };

            var json    = JsonSerializer.Serialize(payloadObj, _jsonOpts);
            _logger.LogDebug("[MQTT] Payload: {json}", json);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TOPIC_FILE)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogInformation("[MQTT] Publicado en {topic} — compilacion_id={id}, instrucciones={n}",
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

    // DTO interno para serializar con la clave "params" correcta
    private class InstruccionMqttDto
    {
        public string comando { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public Dictionary<string, int> @params { get; set; } = new();
    }
}
