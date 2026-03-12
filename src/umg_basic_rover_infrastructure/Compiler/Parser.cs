namespace umg_basic_rover_infrastructure.Compiler;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private readonly List<string> _errores = new();
    private readonly List<NodoInstruccion> _instrucciones = new();

    public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

    public (List<NodoInstruccion> instrucciones, List<string> errores) Parse()
    {
        try { ParsePrograma(); }
        catch (ParseException ex) { _errores.Add(ex.Message); }
        return (_instrucciones, _errores);
    }

    private void ParsePrograma()
    {
        Consumir(TokenType.KEYWORD, "PROGRAM", "Se esperaba 'PROGRAM' al inicio del programa.");
        ConsumirTipo(TokenType.IDENTIFIER, "Se esperaba el nombre del programa después de 'PROGRAM'. Ej: PROGRAM mi_ruta");
        Consumir(TokenType.KEYWORD, "BEGIN", "Se esperaba 'BEGIN' después del nombre del programa.");
        ParseListaInstrucciones();
        Consumir(TokenType.KEYWORD, "END", "Se esperaba 'END' para cerrar el programa.");
        Consumir(TokenType.PUNCTUATION, ".", "Se esperaba '.' después de 'END'. Ej: END.");
        if (Actual().Tipo != TokenType.EOF)
            _errores.Add($"Error sintáctico en línea {Actual().Linea}: código inesperado '{Actual().Lexema}' después de END.");
    }

    private void ParseListaInstrucciones()
    {
        if (Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END")
        {
            _errores.Add("Error sintáctico: El programa no puede estar vacío. Debe tener al menos una instrucción.");
            return;
        }
        while (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
        {
            if (Actual().Tipo == TokenType.EOF) { _errores.Add("Error sintáctico: se llegó al final sin encontrar 'END'."); break; }
            var nodo = ParseInstruccion();
            if (nodo != null) _instrucciones.Add(nodo);
            if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";")
                Avanzar();
            else if (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
            {
                _errores.Add($"Error sintáctico en línea {Actual().Linea}: se esperaba ';' después de la instrucción, se encontró '{Actual().Lexema}'.");
                while (!EsFin() && !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
                       !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END")) Avanzar();
                if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") Avanzar();
            }
        }
    }

    private NodoInstruccion? ParseInstruccion()
    {
        var t = Actual();
        if (t.Tipo == TokenType.KEYWORD && t.Lexema == "girar")       return ParseInstruccionCombinada();
        if (t.Tipo == TokenType.KEYWORD && EsMovimiento(t.Lexema))    return ParseInstruccionSimple();
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "circulo" or "cuadrado") return ParseInstruccionSimple();
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "rotar" or "caminar" or "moonwalk") return ParseInstruccionSimple();

        _errores.Add($"Error sintáctico en línea {t.Linea}: instrucción no reconocida '{t.Lexema}'. " +
                     "Instrucciones válidas: avanzar_vlts, avanzar_ctms, avanzar_mts, girar, circulo, cuadrado, rotar, caminar, moonwalk.");
        while (!EsFin() && !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
               !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END")) Avanzar();
        return null;
    }

    private NodoInstruccion ParseInstruccionSimple()
    {
        var nombre = Actual().Lexema; var linea = Actual().Linea; Avanzar();
        Consumir(TokenType.PARENTHESIS, "(", $"Se esperaba '(' después de '{nombre}' en línea {linea}.");
        var param = ConsumirTipo(TokenType.INTEGER, $"Se esperaba un entero como parámetro en '{nombre}(...)' en línea {linea}.");
        Consumir(TokenType.PARENTHESIS, ")", $"Se esperaba ')' para cerrar '{nombre}(...)' en línea {linea}.");
        return new NodoInstruccion { Nombre = nombre, Parametro = param.Lexema, Raw = $"{nombre}({param.Lexema})", Linea = linea };
    }

    private NodoInstruccion ParseInstruccionCombinada()
    {
        var partes = new List<string>(); var linea = Actual().Linea;
        partes.Add(ParseGirar().Raw);
        while (Actual().Tipo == TokenType.OPERATOR && Actual().Lexema == "+")
        {
            Avanzar();
            var sig = Actual();
            if (sig.Lexema == "girar")          partes.Add(ParseGirar().Raw);
            else if (EsMovimiento(sig.Lexema))  partes.Add(ParseInstruccionSimple().Raw);
            else { _errores.Add($"Error sintáctico en línea {sig.Linea}: después de '+' se esperaba 'girar' o instrucción de avance, se encontró '{sig.Lexema}'."); break; }
        }
        return new NodoInstruccion { Nombre = "combinada", Raw = string.Join(" + ", partes), Linea = linea, EsCombinada = true, Partes = partes };
    }

    private NodoInstruccion ParseGirar()
    {
        var linea = Actual().Linea; Avanzar();
        Consumir(TokenType.PARENTHESIS, "(", $"Se esperaba '(' después de 'girar' en línea {linea}.");
        var param = ConsumirTipo(TokenType.INTEGER, $"Se esperaba -1, 0 o 1 en 'girar()' en línea {linea}.");
        if (!int.TryParse(param.Lexema, out int v) || (v != -1 && v != 0 && v != 1))
            _errores.Add($"Error semántico en línea {linea}: 'girar()' solo acepta -1, 0 o 1. Se encontró '{param.Lexema}'.");
        Consumir(TokenType.PARENTHESIS, ")", $"Se esperaba ')' para cerrar 'girar(...)' en línea {linea}.");
        return new NodoInstruccion { Nombre = "girar", Parametro = param.Lexema, Raw = $"girar({param.Lexema})", Linea = linea };
    }

    private bool EsMovimiento(string n) => n is "avanzar_vlts" or "avanzar_ctms" or "avanzar_mts";
    private Token Actual() => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.EOF, "EOF", "EOF", 0, 0);
    private void Avanzar() => _pos++;
    private bool EsFin()   => _pos >= _tokens.Count || _tokens[_pos].Tipo == TokenType.EOF;

    private Token Consumir(TokenType tipo, string valor, string msg)
    {
        var t = Actual();
        if (t.Tipo == tipo && t.Lexema == valor) { Avanzar(); return t; }
        throw new ParseException($"Error sintáctico en línea {t.Linea}, columna {t.Columna}: {msg} Se encontró '{t.Lexema}'.");
    }

    private Token ConsumirTipo(TokenType tipo, string msg)
    {
        var t = Actual();
        if (t.Tipo == tipo) { Avanzar(); return t; }
        throw new ParseException($"Error sintáctico en línea {t.Linea}, columna {t.Columna}: {msg} Se encontró '{t.Lexema}' (tipo: {t.Tipo}).");
    }
}

public class NodoInstruccion
{
    public string Nombre { get; set; } = string.Empty;
    public string? Parametro { get; set; }
    public string Raw { get; set; } = string.Empty;
    public int Linea { get; set; }
    public bool EsCombinada { get; set; } = false;
    public List<string> Partes { get; set; } = new();
}

public class ParseException : Exception
{
    public ParseException(string message) : base(message) { }
}
