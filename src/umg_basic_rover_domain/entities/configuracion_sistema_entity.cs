namespace umg_basic_rover_domain.entities;

public class configuracion_sistema_entity
{
    public int      id                 { get; set; }
    public string   clave              { get; set; } = string.Empty;
    public string   valor              { get; set; } = string.Empty;
    public string?  descripcion        { get; set; }
    public int?     modificado_por     { get; set; }
    public DateTime fecha_modificacion { get; set; } = DateTime.Now;

    public user_entity? admin { get; set; }
}
