namespace umg_basic_rover_domain.entities;

public class compilacion_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public int? archivo_id { get; set; }
    public int sesion_id { get; set; }
    public string codigo_fuente { get; set; } = string.Empty;
    public string modo_compilacion { get; set; } = "solo_compilar";
    public string resultado { get; set; } = string.Empty;
    public int? tiempo_compilacion_ms { get; set; }
    public DateTime fecha_compilacion { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
    public sesion_entity sesion { get; set; } = null!;
    public archivo_umgpp_entity? archivo { get; set; }
    public ICollection<error_compilacion_entity> errores { get; set; } = new List<error_compilacion_entity>();
    public ICollection<token_lexer_entity> tokens { get; set; } = new List<token_lexer_entity>();
    public ICollection<instruccion_ejecutada_entity> instrucciones { get; set; } = new List<instruccion_ejecutada_entity>();
    public simulacion_entity? simulacion { get; set; }
}
