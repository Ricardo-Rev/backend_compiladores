namespace umg_basic_rover_domain.entities;

public class token_email_verificacion_entity
{
    public int      id             { get; set; }
    public int      usuario_id     { get; set; }
    public string   token          { get; set; } = string.Empty;
    public DateTime expira_en      { get; set; }
    public bool     usado          { get; set; } = false;
    public DateTime fecha_creacion { get; set; } = DateTime.Now;

    public virtual user_entity usuario { get; set; } = null!;
}