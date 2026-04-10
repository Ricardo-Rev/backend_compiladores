namespace umg_basic_rover_application.DTOs;

public class AccesoDto
{
    public int id_ingreso { get; set; }
    public string nickname { get; set; } = string.Empty;
    public string nombre_completo { get; set; } = string.Empty;
    public string? avatar_url { get; set; }
    public string? avatar_base64 { get; set; }
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
    public string nombre_completo { get; set; } = string.Empty;
    public string? avatar_url { get; set; }
    public string? avatar_base64 { get; set; } 
    public string email { get; set; } = string.Empty;
    public bool activo { get; set; }
    public bool email_confirmado { get; set; }
    public DateTime fecha_creacion { get; set; }
    public int total_compilaciones { get; set; }
}

public class CoreografiaCreateDto
{
    public string  nombre        { get; set; } = string.Empty;
    public string? descripcion   { get; set; }
    public string  codigo_fuente { get; set; } = string.Empty;
    public string? cancion_url   { get; set; }
    public string? cancion_nombre { get; set; }
    public int     duracion_min_seg { get; set; } = 180;
}
 
public class CoreografiaUpdateDto
{
    public string? nombre         { get; set; }
    public string? descripcion    { get; set; }
    public string? codigo_fuente  { get; set; }
    public string? cancion_url    { get; set; }
    public string? cancion_nombre { get; set; }
    public int?    duracion_min_seg { get; set; }
}