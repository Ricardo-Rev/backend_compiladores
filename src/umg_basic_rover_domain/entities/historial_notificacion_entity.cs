namespace umg_basic_rover_domain.entities;

public class historial_notificacion_entity
{
    public int      id            { get; set; }
    public int      usuario_id    { get; set; }
    public string   tipo          { get; set; } = "otro";
    public string   canal         { get; set; } = "email";
    public string?  asunto        { get; set; }
    public string?  mensaje       { get; set; }
    public string   estado        { get; set; } = "pendiente";
    public int?     referencia_id { get; set; }
    public DateTime fecha_envio   { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
}
