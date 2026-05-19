namespace umg_basic_rover_application.Contracts;

public interface IMqttService
{
    /// <summary>
    /// Publica la lista de comandos seriales listos para el Arduino.
    /// Cada string es exactamente lo que se enviará por Serial USB:
    ///   "GR:-1", "AV_CM:30", "ROT:2", etc.
    /// La Raspberry solo hace de puente — lee cada línea y la escribe al serial.
    /// </summary>
    Task<bool> PublicarEjecucionAsync(int compilacion_id, List<string> comandos_serial);
    Task<bool> PublicarStopAsync();

    /// <summary>
    /// Publica una acción administrativa para la Raspberry.
    /// El comando lo ejecuta rover-control.service, no el backend directamente.
    /// Acciones permitidas: start_service, stop_service, restart_service, pause, reboot_pi, shutdown_pi.
    /// </summary>
    Task<bool> PublicarSystemControlAsync(string action, string? reason = null);

    bool EstaConectado { get; }
}