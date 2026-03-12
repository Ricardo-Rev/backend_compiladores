namespace umg_basic_rover_domain.entities;

public class codigo_qr_entity
{
    public int      id             { get; set; }
    public int      usuario_id     { get; set; }
    public string   codigo_qr      { get; set; } = string.Empty;
    public string   qr_hash        { get; set; } = string.Empty;
    public bool     activo         { get; set; } = true;
    public DateTime fecha_creacion { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
}
