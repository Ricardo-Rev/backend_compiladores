namespace umg_basic_rover_infrastructure.Compiler;

public class LexerError
{
    public string Codigo     { get; set; } = string.Empty;
    public int    Linea      { get; set; }
    public int    Columna    { get; set; }
    public string Mensaje    { get; set; } = string.Empty;
    public string Sugerencia { get; set; } = string.Empty;
}

public class Lexer
{
    private static readonly HashSet<string> KEYWORDS = new(StringComparer.Ordinal)
    {
        "PROGRAM", "BEGIN", "END", "PUNTO"
    };

    private static readonly HashSet<string> INSTRUCTIONS = new(StringComparer.Ordinal)
    {
        "avanzar_vlts", "avanzar_ctms", "avanzar_mts",
        "girar", "circulo", "cuadrado", "rotar", "caminar", "moonwalk"
    };

    private readonly string           _source;
    private int                       _pos;
    private int                       _linea;
    private int                       _columna;
    private readonly List<Token>      _tokens  = new();
    private readonly List<LexerError> _errores = new();

    public Lexer(string source)
    {
        _source  = source ?? string.Empty;
        _pos     = 0;
        _linea   = 1;
        _columna = 1;
    }

    public (List<Token> tokens, List<LexerError> errores) Tokenize()
    {
        while (!EsFin())
        {
            OmitirEspacios();
            if (EsFin()) break;

            var c = Actual();

            if (c == '\n') { _linea++; _columna = 1; Avanzar(); continue; }
            if (c == '\r') { Avanzar(); continue; }

            if (char.IsDigit(c))              { _tokens.Add(LeerEntero());  continue; }
            if (char.IsLetter(c) || c == '_') { _tokens.Add(LeerPalabra()); continue; }

            if (c == '-' && _tokens.Count > 0 &&
                _tokens[^1].Tipo == TokenType.PARENTHESIS && _tokens[^1].Lexema == "(" &&
                _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
            {
                _tokens.Add(LeerEnteroNegativo());
                continue;
            }

            if (c == '+') { _tokens.Add(new Token(TokenType.OPERATOR,    "+", "+", _linea, _columna)); Avanzar(); continue; }
            if (c == '(') { _tokens.Add(new Token(TokenType.PARENTHESIS, "(", "(", _linea, _columna)); Avanzar(); continue; }
            if (c == ')') { _tokens.Add(new Token(TokenType.PARENTHESIS, ")", ")", _linea, _columna)); Avanzar(); continue; }
            if (c == ';') { _tokens.Add(new Token(TokenType.PUNCTUATION, ";", ";", _linea, _columna)); Avanzar(); continue; }
            if (c == '.') { _tokens.Add(new Token(TokenType.PUNCTUATION, ".", ".", _linea, _columna)); Avanzar(); continue; }

            _errores.Add(new LexerError
            {
                Codigo     = "LEX001",
                Linea      = _linea,
                Columna    = _columna,
                Mensaje    = $"Carácter no reconocido '{c}'.",
                Sugerencia = ObtenerSugerenciaCaracter(c)
            });
            _tokens.Add(new Token(TokenType.UNKNOWN, c.ToString(), c.ToString(), _linea, _columna));
            Avanzar();
        }

        _tokens.Add(new Token(TokenType.EOF, "EOF", "EOF", _linea, _columna));
        return (_tokens, _errores);
    }

    private Token LeerPalabra()
    {
        var lin = _linea; var col = _columna;
        var sb  = new System.Text.StringBuilder();
        while (!EsFin() && (char.IsLetterOrDigit(Actual()) || Actual() == '_'))
        { sb.Append(Actual()); Avanzar(); }
        var lex = sb.ToString();

        if (KEYWORDS.Contains(lex))     return new Token(TokenType.KEYWORD,    lex, lex, lin, col);
        if (INSTRUCTIONS.Contains(lex)) return new Token(TokenType.KEYWORD,    lex, lex, lin, col);

        var similar = BuscarInstruccionSimilar(lex);
        if (similar != null)
        {
            _errores.Add(new LexerError
            {
                Codigo     = "LEX003",
                Linea      = lin,
                Columna    = col,
                Mensaje    = $"Identificador desconocido '{lex}'.",
                Sugerencia = $"¿Quisiste escribir '{similar}'? (distancia de edición: {Levenshtein(lex, similar)})"
            });
        }

        return new Token(TokenType.IDENTIFIER, lex, lex, lin, col);
    }

    private Token LeerEntero()
    {
        var lin = _linea; var col = _columna;
        var sb  = new System.Text.StringBuilder();
        while (!EsFin() && char.IsDigit(Actual())) { sb.Append(Actual()); Avanzar(); }
        var v = sb.ToString();
        return new Token(TokenType.INTEGER, v, v, lin, col);
    }

    private Token LeerEnteroNegativo()
    {
        var lin = _linea; var col = _columna;
        var sb  = new System.Text.StringBuilder();
        sb.Append('-'); Avanzar();
        while (!EsFin() && char.IsDigit(Actual())) { sb.Append(Actual()); Avanzar(); }
        var v = sb.ToString();
        return new Token(TokenType.INTEGER, v, v, lin, col);
    }

    public static int Levenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        for (int j = 1; j <= b.Length; j++)
        {
            int costo = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(
                Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                dp[i - 1, j - 1] + costo
            );
        }

        return dp[a.Length, b.Length];
    }

    private string? BuscarInstruccionSimilar(string lex)
    {
        const int UMBRAL = 3;
        string? mejor    = null;
        int     min_dist = int.MaxValue;

        foreach (var inst in INSTRUCTIONS)
        {
            var dist = Levenshtein(lex.ToLower(), inst.ToLower());
            if (dist < min_dist && dist <= UMBRAL)
            {
                min_dist = dist;
                mejor    = inst;
            }
        }

        return mejor;
    }

    private string ObtenerSugerenciaCaracter(char c) => c switch
    {
        '{' or '}' => "LEX001: UMG++ usa BEGIN y END en lugar de llaves {}.",
        '[' or ']' => "LEX001: UMG++ no usa corchetes. Revisá la sintaxis.",
        '#'        => "LEX002: UMG++ no admite comentarios con #.",
        '/'        => "LEX002: UMG++ no admite comentarios con //.",
        '"' or '\''=> "LEX001: UMG++ no usa cadenas de texto.",
        '='        => "LEX001: UMG++ no tiene asignaciones. Revisá la instrucción.",
        ','        => "LEX001: Las instrucciones no usan comas. Cada instrucción termina con ';'.",
        '@' or '$' => "LEX002: Los símbolos @ y $ no son válidos en UMG++.",
        _          => "LEX001: Revisá la documentación del lenguaje UMG++."
    };

    private char Actual()  => _source[_pos];
    private void Avanzar() { _pos++; _columna++; }
    private bool EsFin()   => _pos >= _source.Length;
    private void OmitirEspacios()
    {
        while (!EsFin() && (Actual() == ' ' || Actual() == '\t')) Avanzar();
    }
}