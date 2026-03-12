namespace umg_basic_rover_domain.entities;

public class autenticacion_facial_entity
{
    public int      id                { get; set; }
    public int      usuario_id        { get; set; }
    public string   encoding_facial   { get; set; } = string.Empty;
    public string?  imagen_referencia { get; set; }
    public bool     activo            { get; set; } = true;
    public DateTime fecha_creacion    { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
}
