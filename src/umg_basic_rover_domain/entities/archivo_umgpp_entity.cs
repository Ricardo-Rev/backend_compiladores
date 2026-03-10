namespace umg_basic_rover_domain.entities;

public class archivo_umgpp_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public string nombre_archivo { get; set; } = string.Empty;
    public string contenido { get; set; } = string.Empty;
    public int version { get; set; } = 1;
    public string? descripcion { get; set; }
    public bool es_coreografia { get; set; } = false;
    public bool activo { get; set; } = true;
    public DateTime fecha_creacion { get; set; } = DateTime.Now;
    public DateTime fecha_modificacion { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
    public ICollection<compilacion_entity> compilaciones { get; set; } = new List<compilacion_entity>();
}
