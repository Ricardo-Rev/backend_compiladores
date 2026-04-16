namespace umg_basic_rover_application.Contracts;

/// <summary>
/// Contrato para notificar al frontend vía WebSocket (SignalR).
/// Se define en Application para que Infrastructure pueda usarlo
/// SIN crear una dependencia circular hacia la capa API.
///
/// Flujo:
///   MqttService (Infrastructure) → IRoverHubNotifier → RoverHubNotifier (API) → SignalR → Frontend
/// </summary>
public interface IRoverHubNotifier
{
    /// <summary>Estado del rover: online / offline / ejecutando</summary>
    Task NotificarStatusAsync(object data);

    /// <summary>ACK de una instrucción completada</summary>
    Task NotificarAckAsync(object data);

    /// <summary>Progreso durante una compilación: instrucción N de N</summary>
    Task NotificarProgresoAsync(object data);
}