namespace umg_basic_rover_domain.entities;

public class preferencias_editor_entity
{
    public int      id                       { get; set; }
    public int      usuario_id               { get; set; }
    public string   tema                     { get; set; } = "dark";
    public int      tamano_fuente            { get; set; } = 14;
    public string   fuente                   { get; set; } = "Fira Code";
    public string   color_keywords           { get; set; } = "#4FC3F7";
    public string   color_commands           { get; set; } = "#87CEEB";
    public string   color_parenthesis        { get; set; } = "#66BB6A";
    public string   color_integers           { get; set; } = "#EF5350";
    public decimal  interlineado             { get; set; } = 1.5m;
    public string   lenguaje_destino_default { get; set; } = "python";
    public DateTime fecha_actualizacion      { get; set; } = DateTime.Now;

    public user_entity usuario { get; set; } = null!;
}
