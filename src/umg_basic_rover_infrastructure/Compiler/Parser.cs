namespace umg_basic_rover_infrastructure.Compiler;

public class CompilerError
{
    public string Codigo     { get; set; } = string.Empty;
    public int    Linea      { get; set; }
    public int    Columna    { get; set; }
    public string Mensaje    { get; set; } = string.Empty;
    public string Sugerencia { get; set; } = string.Empty;
}

public class Parser
{
    private readonly List<Token>           _tokens;
    private int                            _pos;
    private readonly List<CompilerError>   _errores       = new();
    private readonly List<NodoInstruccion> _instrucciones = new();

    private static readonly string[] INSTRUCCIONES_VALIDAS =
    {
        "avanzar_vlts", "avanzar_ctms", "avanzar_mts",
        "girar", "circulo", "cuadrado", "rotar", "caminar", "moonwalk"
    };

    public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

    public (List<NodoInstruccion> instrucciones, List<CompilerError> errores) Parse()
    {
        try { ParsePrograma(); }
        catch (ParseException ex)
        {
            _errores.Add(new CompilerError
            {
                Codigo     = ex.Codigo,
                Linea      = ex.Linea,
                Columna    = ex.Columna,
                Mensaje    = ex.Message,
                Sugerencia = ex.Sugerencia
            });
        }
        return (_instrucciones, _errores);
    }

    private void ParsePrograma()
    {
        Consumir(TokenType.KEYWORD, "PROGRAM", "SIN001",
            "Se esperaba 'PROGRAM' al inicio del programa.",
            "El programa debe comenzar con: PROGRAM nombre_programa");

        ConsumirTipo(TokenType.IDENTIFIER, "SIN002",
            "Se esperaba el nombre del programa después de 'PROGRAM'.",
            "Ejemplo: PROGRAM mi_ruta");

        Consumir(TokenType.KEYWORD, "BEGIN", "SIN003",
            "Se esperaba 'BEGIN' después del nombre del programa.",
            "Estructura: PROGRAM nombre\\nBEGIN\\n  instrucciones\\nEND.");

        ParseListaInstrucciones();

        Consumir(TokenType.KEYWORD, "END", "SIN004",
            "Se esperaba 'END' para cerrar el programa.",
            "El programa debe terminar con: END.");

        Consumir(TokenType.PUNCTUATION, ".", "SIN005",
            "Se esperaba '.' después de 'END'.",
            "Ejemplo correcto: END.");

        if (Actual().Tipo != TokenType.EOF)
            _errores.Add(new CompilerError
            {
                Codigo     = "SIN006",
                Linea      = Actual().Linea,
                Columna    = Actual().Columna,
                Mensaje    = $"Código inesperado '{Actual().Lexema}' después de END.",
                Sugerencia = "Nada debe aparecer después de END."
            });
    }

    private void ParseListaInstrucciones()
    {
        if (Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END")
        {
            _errores.Add(new CompilerError
            {
                Codigo     = "SIN007",
                Linea      = Actual().Linea,
                Columna    = Actual().Columna,
                Mensaje    = "El programa no puede estar vacío.",
                Sugerencia = "Agregá al menos una instrucción entre BEGIN y END."
            });
            return;
        }

        while (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
        {
            if (Actual().Tipo == TokenType.EOF)
            {
                _errores.Add(new CompilerError
                {
                    Codigo     = "SIN008",
                    Linea      = Actual().Linea,
                    Columna    = Actual().Columna,
                    Mensaje    = "Se llegó al final del código sin encontrar 'END'.",
                    Sugerencia = "Verificá que el programa termine con END."
                });
                break;
            }

            var nodo = ParseInstruccion();
            if (nodo != null) _instrucciones.Add(nodo);

            if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";")
                Avanzar();
            else if (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
            {
                _errores.Add(new CompilerError
                {
                    Codigo     = "SIN009",
                    Linea      = Actual().Linea,
                    Columna    = Actual().Columna,
                    Mensaje    = $"Se esperaba ';' después de la instrucción, se encontró '{Actual().Lexema}'.",
                    Sugerencia = "Cada instrucción debe terminar con punto y coma ';'."
                });
                while (!EsFin() &&
                       !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
                       !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
                    Avanzar();
                if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") Avanzar();
            }
        }
    }

    private NodoInstruccion? ParseInstruccion()
    {
        var t = Actual();

        if (t.Tipo == TokenType.KEYWORD && t.Lexema == "girar")
            return ParseInstruccionCombinada();
        if (t.Tipo == TokenType.KEYWORD && EsMovimiento(t.Lexema))
            return ParseInstruccionSimple();
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "circulo" or "cuadrado")
            return ParseInstruccionSimple();
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "rotar" or "caminar" or "moonwalk")
            return ParseInstruccionSimple();

        _errores.Add(new CompilerError
        {
            Codigo     = "SIN010",
            Linea      = t.Linea,
            Columna    = t.Columna,
            Mensaje    = $"Instrucción no reconocida '{t.Lexema}'.",
            Sugerencia = $"Instrucciones válidas: {string.Join(", ", INSTRUCCIONES_VALIDAS)}."
        });

        while (!EsFin() &&
               !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
               !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
            Avanzar();

        return null;
    }

    private NodoInstruccion ParseInstruccionSimple()
    {
        var nombre = Actual().Lexema;
        var linea  = Actual().Linea;
        Avanzar();

        Consumir(TokenType.PARENTHESIS, "(", "SIN011",
            $"Se esperaba '(' después de '{nombre}'.",
            $"Sintaxis correcta: {nombre}(valor);");

        var param = ConsumirTipo(TokenType.INTEGER, "SIN012",
            $"Se esperaba un número entero como parámetro en '{nombre}(...)'.",
            $"Ejemplo: {nombre}(5);");

        Consumir(TokenType.PARENTHESIS, ")", "SIN013",
            $"Se esperaba ')' para cerrar '{nombre}(...)'.",
            "Verificá que los paréntesis estén balanceados.");

        return new NodoInstruccion
        {
            Nombre    = nombre,
            Parametro = param.Lexema,
            Raw       = $"{nombre}({param.Lexema})",
            Linea     = linea
        };
    }

    private NodoInstruccion ParseInstruccionCombinada()
    {
        var partes = new List<string>();
        var linea  = Actual().Linea;
        partes.Add(ParseGirar().Raw);

        while (Actual().Tipo == TokenType.OPERATOR && Actual().Lexema == "+")
        {
            Avanzar();
            var sig = Actual();
            if (sig.Lexema == "girar")
                partes.Add(ParseGirar().Raw);
            else if (EsMovimiento(sig.Lexema))
                partes.Add(ParseInstruccionSimple().Raw);
            else
            {
                _errores.Add(new CompilerError
                {
                    Codigo     = "SIN014",
                    Linea      = sig.Linea,
                    Columna    = sig.Columna,
                    Mensaje    = $"Después de '+' se esperaba 'girar' o instrucción de avance, se encontró '{sig.Lexema}'.",
                    Sugerencia = "Las instrucciones combinadas usan: girar(n) + avanzar_mts(n)"
                });
                break;
            }
        }

        return new NodoInstruccion
        {
            Nombre      = "combinada",
            Raw         = string.Join(" + ", partes),
            Linea       = linea,
            EsCombinada = true,
            Partes      = partes
        };
    }

    private NodoInstruccion ParseGirar()
    {
        var linea = Actual().Linea;
        var col   = Actual().Columna;
        Avanzar();

        Consumir(TokenType.PARENTHESIS, "(", "SIN011",
            "Se esperaba '(' después de 'girar'.",
            "Sintaxis: girar(-1), girar(0) o girar(1)");

        var param = ConsumirTipo(TokenType.INTEGER, "SIN012",
            "Se esperaba -1, 0 o 1 en 'girar()'.",
            "girar(-1) = izquierda | girar(0) = recto | girar(1) = derecha");

        if (!int.TryParse(param.Lexema, out int v) || (v != -1 && v != 0 && v != 1))
            _errores.Add(new CompilerError
            {
                Codigo     = "SEM001",
                Linea      = linea,
                Columna    = col,
                Mensaje    = $"'girar()' solo acepta -1, 0 o 1. Se encontró '{param.Lexema}'.",
                Sugerencia = "girar(-1) = izquierda | girar(0) = recto | girar(1) = derecha"
            });

        Consumir(TokenType.PARENTHESIS, ")", "SIN013",
            "Se esperaba ')' para cerrar 'girar(...)'.",
            "Verificá que los paréntesis estén balanceados.");

        return new NodoInstruccion
        {
            Nombre    = "girar",
            Parametro = param.Lexema,
            Raw       = $"girar({param.Lexema})",
            Linea     = linea
        };
    }

    private bool  EsMovimiento(string n) => n is "avanzar_vlts" or "avanzar_ctms" or "avanzar_mts";
    private Token Actual() => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.EOF, "EOF", "EOF", 0, 0);
    private void  Avanzar() => _pos++;
    private bool  EsFin()   => _pos >= _tokens.Count || _tokens[_pos].Tipo == TokenType.EOF;

    private Token Consumir(TokenType tipo, string valor, string codigo, string msg, string sugerencia = "")
    {
        var t = Actual();
        if (t.Tipo == tipo && t.Lexema == valor) { Avanzar(); return t; }
        throw new ParseException(
            $"{msg} Se encontró '{t.Lexema}'.",
            codigo, t.Linea, t.Columna, sugerencia);
    }

    private Token ConsumirTipo(TokenType tipo, string codigo, string msg, string sugerencia = "")
    {
        var t = Actual();
        if (t.Tipo == tipo) { Avanzar(); return t; }
        throw new ParseException(
            $"{msg} Se encontró '{t.Lexema}' (tipo: {t.Tipo}).",
            codigo, t.Linea, t.Columna, sugerencia);
    }
}

public class NodoInstruccion
{
    public string       Nombre      { get; set; } = string.Empty;
    public string?      Parametro   { get; set; }
    public string       Raw         { get; set; } = string.Empty;
    public int          Linea       { get; set; }
    public bool         EsCombinada { get; set; } = false;
    public List<string> Partes      { get; set; } = new();
}

public class ParseException : Exception
{
    public string Codigo     { get; }
    public int    Linea      { get; }
    public int    Columna    { get; }
    public string Sugerencia { get; }

    public ParseException(string message, string codigo = "SIN000", int linea = 0, int columna = 0, string sugerencia = "")
        : base(message)
    {
        Codigo     = codigo;
        Linea      = linea;
        Columna    = columna;
        Sugerencia = sugerencia;
    }
}