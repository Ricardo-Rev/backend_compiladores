namespace umg_basic_rover_domain.entities;

public class instruccion_umgpp_entity
{
    public int id { get; set; }
    public string nombre_instruccion { get; set; } = string.Empty;
    public string categoria { get; set; } = string.Empty;
    public string descripcion { get; set; } = string.Empty;
    public string sintaxis { get; set; } = string.Empty;
    public int? parametro_n_min { get; set; }
    public int? parametro_n_max { get; set; }
    public bool permite_cero { get; set; } = false;
    public bool activo { get; set; } = true;

    public ICollection<instruccion_ejecutada_entity> instrucciones_ejecutadas { get; set; } = new List<instruccion_ejecutada_entity>();
}
