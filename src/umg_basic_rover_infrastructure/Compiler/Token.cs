namespace umg_basic_rover_infrastructure.Compiler;

public class Token
{
    public TokenType Tipo { get; set; }
    public string Lexema { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public int Linea { get; set; }
    public int Columna { get; set; }

    public Token(TokenType tipo, string lexema, string valor, int linea, int columna)
    {
        Tipo    = tipo;
        Lexema  = lexema;
        Valor   = valor;
        Linea   = linea;
        Columna = columna;
    }

    public override string ToString()
        => $"[{Tipo}] '{Lexema}' (Línea {Linea}, Col {Columna})";
}
