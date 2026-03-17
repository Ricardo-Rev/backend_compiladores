namespace umg_basic_rover_infrastructure.Compiler;

// ============================================================
//  Lexer.cs — Analizador Léxico UMG++ 
//
//  FASE 1 del compilador. Convierte el código fuente en una
//  secuencia de tokens reconocibles por el Parser.
//
//  GRAMÁTICA LÉXICA UMG++:
//  ─────────────────────────────────────────────────────────
//  KEYWORD      → PROGRAM | BEGIN | END | PUNTO
//  INSTRUCTION  → avanzar_vlts | avanzar_ctms | avanzar_mts
//               | girar | circulo | cuadrado | rotar
//               | caminar | moonwalk
//  INTEGER      → [-]? [0-9]+
//  IDENTIFIER   → [a-zA-Z_][a-zA-Z0-9_]*
//  OPERATOR     → +
//  PARENTHESIS  → ( | )
//  PUNCTUATION  → ; | .
//  EOF          → fin del archivo
//
//  ERRORES LÉXICOS DETECTADOS:
//  ─────────────────────────────────────────────────────────
//  - Carácter no reconocido en el lenguaje
//  - Número con formato inválido (ej: 1.5, 1e3)
//  - Símbolo especial no permitido (@, #, $, etc.)
// ============================================================

public class Lexer
{
    // Palabras reservadas del lenguaje UMG++
    private static readonly HashSet<string> KEYWORDS = new(StringComparer.Ordinal)
    {
        "PROGRAM", "BEGIN", "END", "PUNTO"
    };

    // Instrucciones válidas del DSL UMG++
    private static readonly HashSet<string> INSTRUCTIONS = new(StringComparer.Ordinal)
    {
        "avanzar_vlts", "avanzar_ctms", "avanzar_mts",
        "girar", "circulo", "cuadrado", "rotar", "caminar", "moonwalk"
    };

    private readonly string        _source;
    private int                    _pos;
    private int                    _linea;
    private int                    _columna;
    private readonly List<Token>   _tokens  = new();
    private readonly List<ErrorLexico> _errores = new();

    public Lexer(string source)
    {
        _source  = source ?? string.Empty;
        _pos     = 0;
        _linea   = 1;
        _columna = 1;
    }

    // ── TOKENIZAR ────────────────────────────────────────────
    public (List<Token> tokens, List<string> errores) Tokenize()
    {
        while (!EsFin())
        {
            OmitirEspacios();
            if (EsFin()) break;

            var c = Actual();

            // Salto de línea
            if (c == '\n') { _linea++; _columna = 1; Avanzar(); continue; }
            if (c == '\r') { Avanzar(); continue; }

            // Número entero
            if (char.IsDigit(c)) { _tokens.Add(LeerEntero()); continue; }

            // Identificador o palabra reservada
            if (char.IsLetter(c) || c == '_') { _tokens.Add(LeerPalabra()); continue; }

            // Número negativo dentro de paréntesis
            if (c == '-' && _tokens.Count > 0 &&
                _tokens[^1].Tipo == TokenType.PARENTHESIS &&
                _tokens[^1].Lexema == "(" &&
                _pos + 1 < _source.Length &&
                char.IsDigit(_source[_pos + 1]))
            {
                _tokens.Add(LeerEnteroNegativo());
                continue;
            }

            // Número decimal — no soportado, error específico
            if (c == '-' && _pos + 1 < _source.Length && _source[_pos + 1] == '.')
            {
                RegistrarError(
                    codigo: "LEX001",
                    mensaje: $"Número decimal no permitido en línea {_linea}, columna {_columna}.",
                    sugerencia: "UMG++ solo acepta números enteros. Use 'girar(1)' en lugar de 'girar(1.5)'."
                );
                Avanzar(); continue;
            }

            // Operadores y símbolos válidos
            if (c == '+') { _tokens.Add(new Token(TokenType.OPERATOR,    "+", "+", _linea, _columna)); Avanzar(); continue; }
            if (c == '(') { _tokens.Add(new Token(TokenType.PARENTHESIS, "(", "(", _linea, _columna)); Avanzar(); continue; }
            if (c == ')') { _tokens.Add(new Token(TokenType.PARENTHESIS, ")", ")", _linea, _columna)); Avanzar(); continue; }
            if (c == ';') { _tokens.Add(new Token(TokenType.PUNCTUATION, ";", ";", _linea, _columna)); Avanzar(); continue; }
            if (c == '.') { _tokens.Add(new Token(TokenType.PUNCTUATION, ".", ".", _linea, _columna)); Avanzar(); continue; }

            // Carácter no reconocido — error específico con sugerencia
            RegistrarError(
                codigo: "LEX002",
                mensaje: $"Carácter no reconocido '{c}' en línea {_linea}, columna {_columna}.",
                sugerencia: ObtenerSugerenciaCaracter(c)
            );
            _tokens.Add(new Token(TokenType.UNKNOWN, c.ToString(), c.ToString(), _linea, _columna));
            Avanzar();
        }

        _tokens.Add(new Token(TokenType.EOF, "EOF", "EOF", _linea, _columna));

        // Convertir errores a strings con formato detallado
        var errores_str = _errores.Select(e =>
            $"[{e.Codigo}] Error léxico en línea {e.Linea}, columna {e.Columna}: {e.Mensaje} → {e.Sugerencia}"
        ).ToList();

        return (_tokens, errores_str);
    }

    // ── LEER PALABRA (keyword o identificador) ───────────────
    private Token LeerPalabra()
    {
        var lin = _linea;
        var col = _columna;
        var sb  = new System.Text.StringBuilder();

        while (!EsFin() && (char.IsLetterOrDigit(Actual()) || Actual() == '_'))
        {
            sb.Append(Actual());
            Avanzar();
        }

        var lex = sb.ToString();

        if (KEYWORDS.Contains(lex))     return new Token(TokenType.KEYWORD,     lex, lex, lin, col);
        if (INSTRUCTIONS.Contains(lex)) return new Token(TokenType.KEYWORD,     lex, lex, lin, col);

        // Identificador desconocido — puede ser error de escritura
        if (lex.Length > 2)
        {
            var similar = BuscarInstruccionSimilar(lex);
            if (similar != null)
            {
                RegistrarError(
                    codigo: "LEX003",
                    mensaje: $"Identificador desconocido '{lex}' en línea {lin}, columna {col}.",
                    sugerencia: $"¿Quisiste escribir '{similar}'?",
                    linea: lin, columna: col
                );
            }
        }

        return new Token(TokenType.IDENTIFIER, lex, lex, lin, col);
    }

    // ── LEER ENTERO POSITIVO ─────────────────────────────────
    private Token LeerEntero()
    {
        var lin = _linea;
        var col = _columna;
        var sb  = new System.Text.StringBuilder();

        while (!EsFin() && char.IsDigit(Actual()))
        {
            sb.Append(Actual());
            Avanzar();
        }

        // Detectar número decimal accidental (ej: 1.5)
        if (!EsFin() && Actual() == '.')
        {
            var v_parcial = sb.ToString();
            RegistrarError(
                codigo: "LEX004",
                mensaje: $"Número decimal '{v_parcial}.x' no permitido en línea {lin}, columna {col}.",
                sugerencia: $"Use el entero '{v_parcial}' sin decimales.",
                linea: lin, columna: col
            );
            // Consumir el punto y lo que sigue
            Avanzar();
            while (!EsFin() && char.IsDigit(Actual())) Avanzar();
        }

        var v = sb.ToString();
        return new Token(TokenType.INTEGER, v, v, lin, col);
    }

    // ── LEER ENTERO NEGATIVO ─────────────────────────────────
    private Token LeerEnteroNegativo()
    {
        var lin = _linea;
        var col = _columna;
        var sb  = new System.Text.StringBuilder();

        sb.Append('-');
        Avanzar();

        while (!EsFin() && char.IsDigit(Actual()))
        {
            sb.Append(Actual());
            Avanzar();
        }

        var v = sb.ToString();
        return new Token(TokenType.INTEGER, v, v, lin, col);
    }

    // ── REGISTRAR ERROR ───────────────────────────────────────
    private void RegistrarError(string codigo, string mensaje, string sugerencia, int? linea = null, int? columna = null)
    {
        _errores.Add(new ErrorLexico
        {
            Codigo     = codigo,
            Mensaje    = mensaje,
            Sugerencia = sugerencia,
            Linea      = linea ?? _linea,
            Columna    = columna ?? _columna
        });
    }

    // ── SUGERENCIA POR CARÁCTER ───────────────────────────────
    private static string ObtenerSugerenciaCaracter(char c) => c switch
    {
        '{' or '}' => "UMG++ usa BEGIN y END en lugar de llaves {}.",
        '[' or ']' => "UMG++ no usa corchetes. Revise la sintaxis.",
        '"' or '\'' => "UMG++ no usa cadenas de texto.",
        '@' or '#' => "Los símbolos @ y # no son válidos en UMG++.",
        ',' => "Use ';' para separar instrucciones, no ','.",
        ':' => "UMG++ no usa ':'. Revise la sintaxis.",
        '/' => "UMG++ no tiene operador de división.",
        '*' => "UMG++ no tiene operador de multiplicación.",
        '%' => "UMG++ no tiene operador de módulo.",
        _   => $"El carácter '{c}' no pertenece al lenguaje UMG++."
    };

    // ── BÚSQUEDA DE INSTRUCCIÓN SIMILAR ──────────────────────
    private static string? BuscarInstruccionSimilar(string lex)
    {
        var todas = new[]
        {
            "avanzar_vlts", "avanzar_ctms", "avanzar_mts",
            "girar", "circulo", "cuadrado", "rotar", "caminar", "moonwalk",
            "PROGRAM", "BEGIN", "END"
        };

        foreach (var inst in todas)
        {
            if (CalcularDistanciaLevenshtein(lex.ToLower(), inst.ToLower()) <= 2)
                return inst;
        }
        return null;
    }

    // ── DISTANCIA DE LEVENSHTEIN (para sugerencias) ──────────
    private static int CalcularDistanciaLevenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var matriz = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) matriz[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) matriz[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                matriz[i, j] = Math.Min(
                    Math.Min(matriz[i - 1, j] + 1, matriz[i, j - 1] + 1),
                    matriz[i - 1, j - 1] + costo
                );
            }
        }

        return matriz[a.Length, b.Length];
    }

    // ── HELPERS ───────────────────────────────────────────────
    private char Actual()  => _source[_pos];
    private void Avanzar() { _pos++; _columna++; }
    private bool EsFin()   => _pos >= _source.Length;

    private void OmitirEspacios()
    {
        while (!EsFin() && (Actual() == ' ' || Actual() == '\t'))
            Avanzar();
    }
}

// ── MODELO DE ERROR LÉXICO ────────────────────────────────────
public class ErrorLexico
{
    public string Codigo     { get; set; } = string.Empty;
    public string Mensaje    { get; set; } = string.Empty;
    public string Sugerencia { get; set; } = string.Empty;
    public int    Linea      { get; set; }
    public int    Columna    { get; set; }
}