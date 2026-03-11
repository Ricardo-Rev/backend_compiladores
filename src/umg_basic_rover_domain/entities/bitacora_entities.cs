namespace umg_basic_rover_domain.entities;

public class bitacora_acceso_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public string metodo_login { get; set; } = "password";
    public string? ip_origen { get; set; }
    public string? user_agent { get; set; }
    public DateTime fecha_ingreso { get; set; } = DateTime.Now;
    public DateTime? fecha_salida { get; set; }

    public user_entity usuario { get; set; } = null!;
}

public class bitacora_accion_entity
{
    public int id { get; set; }
    public int usuario_id { get; set; }
    public int sesion_id { get; set; }
    public string tipo_accion { get; set; } = string.Empty;
    public string? descripcion { get; set; }
    public DateTime fecha_accion { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
    public sesion_entity sesion { get; set; } = null!;
}
