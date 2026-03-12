namespace umg_basic_rover_domain.entities;

public class token_lexer_entity
{
    public int id { get; set; }
    public int compilacion_id { get; set; }
    public int numero_linea { get; set; }
    public int numero_columna { get; set; }
    public string tipo_token { get; set; } = string.Empty;
    public string lexema { get; set; } = string.Empty;
    public string? valor { get; set; }

    public compilacion_entity compilacion { get; set; } = null!;
}
