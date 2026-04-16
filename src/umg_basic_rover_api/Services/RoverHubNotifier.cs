using Microsoft.AspNetCore.SignalR;
using umg_basic_rover_api.Hubs;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_api.Services;

/// <summary>
/// Implementación de IRoverHubNotifier usando SignalR.
/// Vive en la capa API porque es la única que puede referenciar RoverHub.
/// Se registra como Singleton para que MqttService (Singleton) pueda inyectarlo.
/// </summary>
public class RoverHubNotifier : IRoverHubNotifier
{
    private readonly IHubContext<RoverHub> _hub;

    public RoverHubNotifier(IHubContext<RoverHub> hub)
    {
        _hub = hub;
    }

    public async Task NotificarStatusAsync(object data) =>
        await _hub.Clients.All.SendAsync("RoverStatus", data);

    public async Task NotificarAckAsync(object data) =>
        await _hub.Clients.All.SendAsync("RoverAck", data);

    public async Task NotificarProgresoAsync(object data) =>
        await _hub.Clients.All.SendAsync("RoverProgreso", data);
}