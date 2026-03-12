namespace umg_basic_rover_domain.entities;

public class simulacion_entity
{
    public int id { get; set; }
    public int compilacion_id { get; set; }
    public int usuario_id { get; set; }
    public string trayectoria_json { get; set; } = string.Empty;
    public int? duracion_estimada_seg { get; set; }
    public decimal? distancia_total_cm { get; set; }
    public DateTime fecha_simulacion { get; set; } = DateTime.Now;

    public compilacion_entity compilacion { get; set; } = null!;
    public user_entity usuario { get; set; } = null!;
}
