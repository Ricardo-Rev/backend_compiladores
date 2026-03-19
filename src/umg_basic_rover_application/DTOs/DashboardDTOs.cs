namespace umg_basic_rover_application.DTOs;

public class AccesoDto
{
    public int id_ingreso { get; set; }
    public string nickname { get; set; } = string.Empty;
    public string? avatar_url { get; set; }
    public string? avatar_base64 { get; set; }  // ← agregar esto
    public string metodo_login { get; set; } = string.Empty;
    public string? ip_origen { get; set; }
    public DateTime fecha_ingreso { get; set; }
    public DateTime? fecha_salida { get; set; }
    public string estado => fecha_salida.HasValue ? "cerrada" : "activa";
}

public class AspiranteDto
{
    public int id_elegido { get; set; }
    public string nickname { get; set; } = string.Empty;
    public string? avatar_url { get; set; }
    public string? avatar_base64 { get; set; }  // ← agregar esto
    public string email { get; set; } = string.Empty;
    public bool activo { get; set; }
    public DateTime fecha_creacion { get; set; }
    public int total_compilaciones { get; set; }
}
