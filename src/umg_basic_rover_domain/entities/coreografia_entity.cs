namespace umg_basic_rover_domain.entities;

public class coreografia_entity
{
    public int      id               { get; set; }
    public string   nombre           { get; set; } = string.Empty;
    public string?  descripcion      { get; set; }
    public string   codigo_fuente    { get; set; } = string.Empty;
    public string?  cancion_url      { get; set; }
    public string?  cancion_nombre   { get; set; }
    public int      duracion_min_seg { get; set; } = 180;
    public int?     creado_por       { get; set; }
    public bool     activa           { get; set; } = true;
    public DateTime fecha_creacion   { get; set; } = DateTime.Now;

    public user_entity?                              admin       { get; set; }
    public ICollection<coreografia_ejecutada_entity> ejecuciones { get; set; } = new List<coreografia_ejecutada_entity>();
}
