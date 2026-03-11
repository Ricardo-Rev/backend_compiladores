namespace umg_basic_rover_domain.entities;

public class user_entity
{
    public int id { get; set; }
    public string usuario { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public bool email_confirmado { get; set; } = false;
    public string nombre_completo { get; set; } = string.Empty;
    public string password_hash { get; set; } = string.Empty;
    public string telefono { get; set; } = string.Empty;
    public bool telefono_confirmado { get; set; } = false;
    public string? avatar_url { get; set; }
    public string? avatar_base64 { get; set; }
    public string rol { get; set; } = "conductor";
    public bool activo { get; set; } = true;
    public DateTime fecha_creacion { get; set; } = DateTime.Now;

    public ICollection<sesion_entity> sesiones { get; set; } = new List<sesion_entity>();
}