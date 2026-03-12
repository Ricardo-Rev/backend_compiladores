namespace umg_basic_rover_domain.entities;

public class instruccion_ejecutada_entity
{
    public int id { get; set; }
    public int compilacion_id { get; set; }
    public int numero_orden { get; set; }
    public int instruccion_id { get; set; }
    public int? parametro_n { get; set; }
    public int? parametro_r { get; set; }
    public int? parametro_l { get; set; }
    public string instruccion_raw { get; set; } = string.Empty;

    public compilacion_entity compilacion { get; set; } = null!;
    public instruccion_umgpp_entity instruccion { get; set; } = null!;
}
