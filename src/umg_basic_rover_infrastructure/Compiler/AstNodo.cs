namespace umg_basic_rover_infrastructure.Compiler;

public class AstNodo
{
    public string        tipo  { get; set; } = string.Empty;
    public string?       valor { get; set; }
    public int           linea { get; set; }
    public List<AstNodo> hijos { get; set; } = new();
}