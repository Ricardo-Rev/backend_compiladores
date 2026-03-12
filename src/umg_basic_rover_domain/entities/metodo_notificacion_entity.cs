namespace umg_basic_rover_domain.entities;

public class metodo_notificacion_entity
{
    public int      id                { get; set; }
    public int      usuario_id        { get; set; }
    public string   tipo_notificacion { get; set; } = "email";
    public string   destino           { get; set; } = string.Empty;
    public bool     activo            { get; set; } = true;
    public DateTime fecha_creacion    { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
}
