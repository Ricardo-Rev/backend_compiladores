namespace umg_basic_rover_domain.entities;

public class credencial_pdf_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public string? archivo_url { get; set; }
    public string? archivo_base64 { get; set; }
    public string? firma_electronica { get; set; }
    public string canal_envio { get; set; } = "ambos";
    public string estado_envio { get; set; } = "pendiente";
    public DateTime fecha_generacion { get; set; } = DateTime.Now;
    public DateTime? fecha_envio { get; set; }

    public user_entity usuario { get; set; } = null!;
}
