namespace umg_basic_rover_domain.entities;

public class sesion_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public string session_token { get; set; } = string.Empty;
    public string metodo_login { get; set; } = "password";
    public string? ip_origen { get; set; }
    public string? user_agent { get; set; }
    public DateTime fecha_login { get; set; } = DateTime.Now;
    public DateTime fecha_expiracion { get; set; }
    public bool activa { get; set; } = true;

    public user_entity usuario { get; set; } = null!;
}