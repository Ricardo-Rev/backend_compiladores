namespace umg_basic_rover_infrastructure.Compiler;

// ============================================================
//  Parser.cs — Analizador Sintáctico UMG++
//
//  FASE 2 del compilador. Verifica que la secuencia de tokens
//  producida por el Lexer siga la gramática formal del lenguaje.
//
//  ══════════════════════════════════════════════════════════
//  GRAMÁTICA FORMAL BNF DE UMG++ (Backus-Naur Form)
//  ══════════════════════════════════════════════════════════
//
//  <programa>          ::= "PROGRAM" <identificador> "BEGIN"
//                          <lista_instrucciones>
//                          "END" "."
//
//  <lista_instrucciones> ::= <instruccion> (";" <instruccion>)* ";"?
//
//  <instruccion>       ::= <instruccion_simple>
//                        | <instruccion_combinada>
//
//  <instruccion_simple> ::= <nombre_instruccion> "(" <entero> ")"
//
//  <instruccion_combinada> ::= <girar> ("+" (<girar> | <avanzar>))*
//
//  <girar>             ::= "girar" "(" <entero_direccion> ")"
//  <avanzar>           ::= ("avanzar_vlts" | "avanzar_ctms" | "avanzar_mts")
//                          "(" <entero> ")"
//
//  <nombre_instruccion> ::= "avanzar_vlts" | "avanzar_ctms" | "avanzar_mts"
//                         | "girar" | "circulo" | "cuadrado"
//                         | "rotar" | "caminar" | "moonwalk"
//
//  <entero>            ::= [-]? [0-9]+
//  <entero_direccion>  ::= "-1" | "0" | "1"
//  <identificador>     ::= [a-zA-Z_][a-zA-Z0-9_]*
//
//  ══════════════════════════════════════════════════════════
//  SEMÁNTICA OPERACIONAL:
//  ══════════════════════════════════════════════════════════
//  avanzar_vlts(N) → El rover avanza N vueltas de rueda
//                    N > 0 = adelante, N < 0 = atrás
//  avanzar_ctms(N) → El rover avanza N centímetros
//  avanzar_mts(N)  → El rover avanza N metros
//  girar(1)        → Activa motor izquierdo → gira a la derecha
//  girar(-1)       → Activa motor derecho   → gira a la izquierda
//  girar(0)        → Activa ambos motores   → avanza recto
//  circulo(R)      → El rover traza un círculo de radio R cm
//  cuadrado(L)     → El rover traza un cuadrado de lado L cm
//  rotar(N)        → El rover rota N vueltas sobre su eje
//  caminar(N)      → El rover avanza N pasos
//  moonwalk(N)     → El rover retrocede N pasos (moonwalk)
//
//  ERRORES SINTÁCTICOS DETECTADOS:
//  ─────────────────────────────────────────────────────────
//  SIN001 → Falta PROGRAM al inicio
//  SIN002 → Falta nombre del programa
//  SIN003 → Falta BEGIN
//  SIN004 → Programa vacío (sin instrucciones)
//  SIN005 → Instrucción no reconocida
//  SIN006 → Falta paréntesis de apertura
//  SIN007 → Falta parámetro entero
//  SIN008 → Falta paréntesis de cierre
//  SIN009 → Falta punto y coma
//  SIN010 → Falta END
//  SIN011 → Falta punto final después de END
//  SIN012 → Código inesperado después de END
// ============================================================

public class Parser
{
    private readonly List<Token>          _tokens;
    private int                           _pos;
    private readonly List<string>         _errores      = new();
    private readonly List<NodoInstruccion> _instrucciones = new();

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos    = 0;
    }

    // ── PARSE PRINCIPAL ──────────────────────────────────────
    public (List<NodoInstruccion> instrucciones, List<string> errores) Parse()
    {
        try
        {
            ParsePrograma();
        }
        catch (ParseException ex)
        {
            _errores.Add(ex.Message);
        }
        return (_instrucciones, _errores);
    }

    // ── PROGRAMA ─────────────────────────────────────────────
    // <programa> ::= "PROGRAM" <identificador> "BEGIN" <lista> "END" "."
    private void ParsePrograma()
    {
        Consumir(TokenType.KEYWORD, "PROGRAM",
            "[SIN001] Se esperaba 'PROGRAM' al inicio del programa. " +
            "Ejemplo: PROGRAM mi_programa");

        ConsumirTipo(TokenType.IDENTIFIER,
            "[SIN002] Se esperaba el nombre del programa después de 'PROGRAM'. " +
            "Ejemplo: PROGRAM mi_ruta");

        Consumir(TokenType.KEYWORD, "BEGIN",
            "[SIN003] Se esperaba 'BEGIN' para iniciar el bloque de instrucciones. " +
            "Ejemplo: PROGRAM mi_ruta\\nBEGIN");

        ParseListaInstrucciones();

        Consumir(TokenType.KEYWORD, "END",
            "[SIN010] Se esperaba 'END' para cerrar el programa. " +
            "¿Falta cerrar el bloque?");

        Consumir(TokenType.PUNCTUATION, ".",
            "[SIN011] Se esperaba '.' después de 'END'. " +
            "El programa debe terminar con 'END.'");

        if (Actual().Tipo != TokenType.EOF)
            _errores.Add(
                $"[SIN012] Error sintáctico en línea {Actual().Linea}: " +
                $"código inesperado '{Actual().Lexema}' después de END. " +
                "El programa debe terminar después de 'END.'");
    }

    // ── LISTA DE INSTRUCCIONES ────────────────────────────────
    // <lista> ::= <instruccion> (";" <instruccion>)* ";"?
    private void ParseListaInstrucciones()
    {
        if (Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END")
        {
            _errores.Add(
                "[SIN004] Error sintáctico: el programa no puede estar vacío. " +
                "Debe tener al menos una instrucción entre BEGIN y END.");
            return;
        }

        while (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
        {
            if (Actual().Tipo == TokenType.EOF)
            {
                _errores.Add(
                    "[SIN010] Error sintáctico: se llegó al final del archivo sin encontrar 'END'. " +
                    "¿Olvidó cerrar el programa con 'END.'?");
                break;
            }

            var nodo = ParseInstruccion();
            if (nodo != null) _instrucciones.Add(nodo);

            if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";")
            {
                Avanzar();
            }
            else if (!(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
            {
                _errores.Add(
                    $"[SIN009] Error sintáctico en línea {Actual().Linea}: " +
                    $"se esperaba ';' después de la instrucción, se encontró '{Actual().Lexema}'. " +
                    "Cada instrucción debe terminar con ';'.");

                // Recuperación de error: avanzar hasta el próximo ';' o 'END'
                while (!EsFin() &&
                       !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
                       !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
                    Avanzar();

                if (Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";")
                    Avanzar();
            }
        }
    }

    // ── INSTRUCCIÓN ───────────────────────────────────────────
    // <instruccion> ::= <instruccion_simple> | <instruccion_combinada>
    private NodoInstruccion? ParseInstruccion()
    {
        var t = Actual();

        // Instrucción combinada: comienza con girar
        if (t.Tipo == TokenType.KEYWORD && t.Lexema == "girar")
            return ParseInstruccionCombinada();

        // Instrucciones de avance
        if (t.Tipo == TokenType.KEYWORD && EsAvance(t.Lexema))
            return ParseInstruccionSimple();

        // Figuras geométricas
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "circulo" or "cuadrado")
            return ParseInstruccionSimple();

        // Movimientos especiales
        if (t.Tipo == TokenType.KEYWORD && t.Lexema is "rotar" or "caminar" or "moonwalk")
            return ParseInstruccionSimple();

        // Instrucción no reconocida
        _errores.Add(
            $"[SIN005] Error sintáctico en línea {t.Linea}, columna {t.Columna}: " +
            $"instrucción no reconocida '{t.Lexema}'. " +
            "Instrucciones válidas: avanzar_vlts, avanzar_ctms, avanzar_mts, " +
            "girar, circulo, cuadrado, rotar, caminar, moonwalk.");

        // Recuperación: avanzar hasta ';' o 'END'
        while (!EsFin() &&
               !(Actual().Tipo == TokenType.PUNCTUATION && Actual().Lexema == ";") &&
               !(Actual().Tipo == TokenType.KEYWORD && Actual().Lexema == "END"))
            Avanzar();

        return null;
    }

    // ── INSTRUCCIÓN SIMPLE ────────────────────────────────────
    // <instruccion_simple> ::= <nombre> "(" <entero> ")"
    private NodoInstruccion ParseInstruccionSimple()
    {
        var nombre = Actual().Lexema;
        var linea  = Actual().Linea;
        Avanzar();

        Consumir(TokenType.PARENTHESIS, "(",
            $"[SIN006] Se esperaba '(' después de '{nombre}' en línea {linea}. " +
            $"Ejemplo: {nombre}(5)");

        var param = ConsumirTipo(TokenType.INTEGER,
            $"[SIN007] Se esperaba un número entero como parámetro en '{nombre}(...)' en línea {linea}. " +
            $"Ejemplo: {nombre}(5)");

        Consumir(TokenType.PARENTHESIS, ")",
            $"[SIN008] Se esperaba ')' para cerrar '{nombre}(...)' en línea {linea}. " +
            $"Ejemplo: {nombre}({param.Lexema})");

        return new NodoInstruccion
        {
            Nombre    = nombre,
            Parametro = param.Lexema,
            Raw       = $"{nombre}({param.Lexema})",
            Linea     = linea
        };
    }

    // ── INSTRUCCIÓN COMBINADA ─────────────────────────────────
    // <combinada> ::= <girar> ("+" (<girar> | <avanzar>))*
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
            else if (EsAvance(sig.Lexema))
                partes.Add(ParseInstruccionSimple().Raw);
            else
            {
                _errores.Add(
                    $"[SIN005] Error sintáctico en línea {sig.Linea}: " +
                    $"después de '+' se esperaba 'girar' o instrucción de avance, " +
                    $"se encontró '{sig.Lexema}'.");
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

    // ── GIRAR ─────────────────────────────────────────────────
    // <girar> ::= "girar" "(" ("-1" | "0" | "1") ")"
    private NodoInstruccion ParseGirar()
    {
        var linea = Actual().Linea;
        Avanzar();

        Consumir(TokenType.PARENTHESIS, "(",
            $"[SIN006] Se esperaba '(' después de 'girar' en línea {linea}. " +
            "Ejemplo: girar(1)");

        var param = ConsumirTipo(TokenType.INTEGER,
            $"[SIN007] Se esperaba -1, 0 o 1 como parámetro en 'girar()' en línea {linea}. " +
            "Use: girar(1)=derecha, girar(-1)=izquierda, girar(0)=recto");

        if (!int.TryParse(param.Lexema, out int v) || v != -1 && v != 0 && v != 1)
            _errores.Add(
                $"[SEM001] Error semántico en línea {linea}: " +
                $"'girar()' solo acepta -1 (izquierda), 0 (recto) o 1 (derecha). " +
                $"Se encontró '{param.Lexema}'.");

        Consumir(TokenType.PARENTHESIS, ")",
            $"[SIN008] Se esperaba ')' para cerrar 'girar(...)' en línea {linea}.");

        return new NodoInstruccion
        {
            Nombre    = "girar",
            Parametro = param.Lexema,
            Raw       = $"girar({param.Lexema})",
            Linea     = linea
        };
    }

    // ── HELPERS ───────────────────────────────────────────────
    private bool EsAvance(string n) =>
        n is "avanzar_vlts" or "avanzar_ctms" or "avanzar_mts";

    private Token Actual() =>
        _pos < _tokens.Count
            ? _tokens[_pos]
            : new Token(TokenType.EOF, "EOF", "EOF", 0, 0);

    private void Avanzar() => _pos++;

    private bool EsFin() =>
        _pos >= _tokens.Count || _tokens[_pos].Tipo == TokenType.EOF;

    private Token Consumir(TokenType tipo, string valor, string msg)
    {
        var t = Actual();
        if (t.Tipo == tipo && t.Lexema == valor) { Avanzar(); return t; }
        throw new ParseException(
            $"Error sintáctico en línea {t.Linea}, columna {t.Columna}: " +
            $"{msg} Se encontró '{t.Lexema}'.");
    }

    private Token ConsumirTipo(TokenType tipo, string msg)
    {
        var t = Actual();
        if (t.Tipo == tipo) { Avanzar(); return t; }
        throw new ParseException(
            $"Error sintáctico en línea {t.Linea}, columna {t.Columna}: " +
            $"{msg} Se encontró '{t.Lexema}' (tipo: {t.Tipo}).");
    }
}

// ── NODO DEL AST ─────────────────────────────────────────────
/// <summary>
/// Representa un nodo del Árbol Sintáctico Abstracto (AST).
/// Cada nodo corresponde a una instrucción UMG++ validada
/// sintácticamente pero aún no validada semánticamente.
/// </summary>
public class NodoInstruccion
{
    public string       Nombre      { get; set; } = string.Empty;
    public string?      Parametro   { get; set; }
    public string       Raw         { get; set; } = string.Empty;
    public int          Linea       { get; set; }
    public bool         EsCombinada { get; set; } = false;
    public List<string> Partes      { get; set; } = new();
}

// ── EXCEPCIÓN DEL PARSER ─────────────────────────────────────
public class ParseException : Exception
{
    public ParseException(string message) : base(message) { }
}