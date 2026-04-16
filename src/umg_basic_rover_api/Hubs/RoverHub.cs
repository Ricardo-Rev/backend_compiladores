using Microsoft.AspNetCore.SignalR;

namespace umg_basic_rover_api.Hubs;

/// <summary>
/// Hub de SignalR para comunicación en tiempo real con el frontend.
///
/// El frontend se conecta a:  wss://tu-backend.railway.app/hubs/rover
///
/// Mensajes que recibe el frontend:
///   "RoverStatus"  → { status, compilacion_id, timestamp }
///   "RoverAck"     → { estado, mensaje, compilacion_id, timestamp }
///   "RoverProgreso"→ { compilacion_id, progreso, total }
/// </summary>
public class RoverHub : Hub
{
    private readonly ILogger<RoverHub> _logger;

    public RoverHub(ILogger<RoverHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[WS] Cliente conectado: {id}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[WS] Cliente desconectado: {id}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
