namespace umg_basic_rover_domain.entities;

public class error_compilacion_entity
{
    public int id { get; set; }
    public int compilacion_id { get; set; }
    public string tipo_error { get; set; } = string.Empty;
    public int? numero_linea { get; set; }
    public int? numero_columna { get; set; }
    public string? token_encontrado { get; set; }
    public string mensaje_error { get; set; } = string.Empty;
    public string? sugerencia { get; set; }

    public compilacion_entity compilacion { get; set; } = null!;
}
