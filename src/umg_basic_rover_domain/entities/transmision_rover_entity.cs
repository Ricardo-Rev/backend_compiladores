namespace umg_basic_rover_domain.entities;

public class transmision_rover_entity
{
    public int id { get; set; }
    public int compilacion_id { get; set; }
    public int usuario_id { get; set; }
    public string? archivo_reducido_url { get; set; }
    public string? archivo_ejecutable_url { get; set; }
    public string lenguaje_destino { get; set; } = "python";
    public string estado_envio { get; set; } = "pendiente";
    public string metodo_envio { get; set; } = "inalambrico";
    public string? mensaje_respuesta { get; set; }
    public DateTime fecha_envio { get; set; } = DateTime.Now;
    public DateTime? fecha_respuesta { get; set; }

    public compilacion_entity compilacion { get; set; } = null!;
    public user_entity usuario { get; set; } = null!;
}
