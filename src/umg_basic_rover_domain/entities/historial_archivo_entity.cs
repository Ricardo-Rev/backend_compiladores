namespace umg_basic_rover_domain.entities;

public class historial_archivo_entity
{
    public int      id             { get; set; }
    public int      archivo_id     { get; set; }
    public int      usuario_id     { get; set; }
    public int      version        { get; set; }
    public string   contenido      { get; set; } = string.Empty;
    public string?  comentario     { get; set; }
    public DateTime fecha_guardado { get; set; } = DateTime.Now;

    public archivo_umgpp_entity archivo { get; set; } = null!;
    public user_entity          usuario { get; set; } = null!;
}
