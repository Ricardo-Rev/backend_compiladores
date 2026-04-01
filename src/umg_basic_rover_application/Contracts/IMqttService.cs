namespace umg_basic_rover_application.Contracts;

public interface IMqttService
{
    Task<bool> PublicarEjecucionAsync(int compilacion_id, List<RoverInstruccionPayload> instrucciones);
    Task<bool> PublicarStopAsync();
    bool EstaConectado { get; }
}

public class RoverInstruccionPayload
{
    public string comando  { get; set; } = string.Empty;
    public Dictionary<string, int> params_ { get; set; } = new();
}