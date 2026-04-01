using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Mqtt;

public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient      _client;
    private readonly MqttClientOptions _options;
    private readonly ILogger<MqttService> _logger;

    private const string TOPIC_FILE = "rover/file/send";
    private const string TOPIC_STOP = "rover/stop";

    public bool EstaConectado => _client.IsConnected;

    public MqttService(IConfiguration config, ILogger<MqttService> logger)
    {
        _logger = logger;

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

            var payload = new
            {
                compilacion_id,
                instrucciones = instrucciones.Select(i => new
                {
                    comando = i.comando,
                    @params = i.params_
                })
            };

            var json    = JsonSerializer.Serialize(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TOPIC_FILE)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogInformation("[MQTT] Publicado en {topic} — compilacion_id={id}", TOPIC_FILE, compilacion_id);
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
                .WithPayload(Encoding.UTF8.GetBytes("{\"stop\":true}"))
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
}