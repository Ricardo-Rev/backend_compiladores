namespace umg_basic_rover_application.DTOs;

public class RoverExecuteRequest
{
    public int compilacion_id { get; set; }
    public string lenguaje_destino { get; set; } = "python";
}

public class RoverExecuteResponse
{
    public bool   exitoso        { get; set; }
    public string mensaje        { get; set; } = string.Empty;
    public int    transmision_id { get; set; }
    public int    compilacion_id { get; set; }
    public int    total_instrucciones { get; set; }
}

public class RoverStopResponse
{
    public bool   exitoso { get; set; }
    public string mensaje { get; set; } = string.Empty;
}

public class RoverSystemControlRequest
{
    public string action { get; set; } = string.Empty;
    public string? reason { get; set; }
}

public class RoverSystemControlResponse
{
    public bool exitoso { get; set; }
    public string mensaje { get; set; } = string.Empty;
    public string action { get; set; } = string.Empty;
    public DateTime timestamp { get; set; } = DateTime.UtcNow;
}
