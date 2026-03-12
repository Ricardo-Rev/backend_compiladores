namespace umg_basic_rover_infrastructure.Compiler;

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

    private readonly string _source;
    private int _pos;
    private int _linea;
    private int _columna;
    private readonly List<Token>  _tokens  = new();
    private readonly List<string> _errores = new();

    public Lexer(string source)
    {
        _source  = source ?? string.Empty;
        _pos     = 0;
        _linea   = 1;
        _columna = 1;
    }

    public (List<Token> tokens, List<string> errores) Tokenize()
    {
        while (!EsFin())
        {
            OmitirEspacios();
            if (EsFin()) break;

            var c = Actual();

            if (c == '\n') { _linea++; _columna = 1; Avanzar(); continue; }
            if (c == '\r') { Avanzar(); continue; }

            if (char.IsDigit(c))                { _tokens.Add(LeerEntero());         continue; }
            if (char.IsLetter(c) || c == '_')   { _tokens.Add(LeerPalabra());        continue; }

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

            _errores.Add($"Error léxico en línea {_linea}, columna {_columna}: carácter no reconocido '{c}'");
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
        return                                 new Token(TokenType.IDENTIFIER, lex, lex, lin, col);
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

    private char Actual()  => _source[_pos];
    private void Avanzar() { _pos++; _columna++; }
    private bool EsFin()   => _pos >= _source.Length;
    private void OmitirEspacios()
    {
        while (!EsFin() && (Actual() == ' ' || Actual() == '\t')) Avanzar();
    }
}
