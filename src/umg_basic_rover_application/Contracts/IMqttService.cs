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
    bool EstaConectado { get; }
}