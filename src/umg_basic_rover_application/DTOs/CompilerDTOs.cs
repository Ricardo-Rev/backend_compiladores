using System.ComponentModel.DataAnnotations;

namespace umg_basic_rover_application.DTOs;

public class CompileRequest
{
    [Required(ErrorMessage = "El código fuente es obligatorio.")]
    [MinLength(10, ErrorMessage = "El código fuente es demasiado corto.")]
    public string codigo_fuente { get; set; } = string.Empty;
    [Required]
    public string modo { get; set; } = "solo_compilar";
    public int? archivo_id { get; set; }
    public string lenguaje_destino { get; set; } = "python";
}

public class CompileResponse
{
    public bool exitoso { get; set; }
    public string resultado { get; set; } = string.Empty;
    public int compilacion_id { get; set; }
    public int tiempo_ms { get; set; }
    public List<TokenDto> tokens { get; set; } = new();
    public List<ErrorDto> errores { get; set; } = new();
    public List<InstruccionDto> instrucciones { get; set; } = new();
    public string? codigo_transpilado { get; set; }
    public SimulacionDto? simulacion { get; set; }
}

public class TokenDto
{
    public int linea { get; set; }
    public int columna { get; set; }
    public string tipo { get; set; } = string.Empty;
    public string lexema { get; set; } = string.Empty;
    public string? valor { get; set; }
}

public class ErrorDto
{
    public string tipo { get; set; } = string.Empty;
    public int? linea { get; set; }
    public int? columna { get; set; }
    public string? token { get; set; }
    public string mensaje { get; set; } = string.Empty;
    public string? sugerencia { get; set; }
}

public class InstruccionDto
{
    public int orden { get; set; }
    public string nombre { get; set; } = string.Empty;
    public string raw { get; set; } = string.Empty;
    public int? parametro_n { get; set; }
    public int? parametro_r { get; set; }
    public int? parametro_l { get; set; }
}

public class SimulacionDto
{
    public int simulacion_id { get; set; }
    public string trayectoria_json { get; set; } = string.Empty;
    public int? duracion_estimada_seg { get; set; }
    public decimal? distancia_total_cm { get; set; }
}

public class CompileHistoryResponse
{
    public int id { get; set; }
    public string resultado { get; set; } = string.Empty;
    public string? modo_compilacion { get; set; }
    public int tiempo_ms { get; set; }
    public DateTime fecha_compilacion { get; set; }
    public int total_instrucciones { get; set; }
}