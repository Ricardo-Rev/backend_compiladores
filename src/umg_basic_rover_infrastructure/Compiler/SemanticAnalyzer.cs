namespace umg_basic_rover_infrastructure.Compiler;

// ============================================================
//  SemanticAnalyzer.cs — Analizador Semántico UMG++
//
//  FASE 3 del compilador. Verifica que los valores de los
//  parámetros sean semánticamente correctos según las reglas
//  del lenguaje UMG++.
//
//  REGLAS SEMÁNTICAS POR INSTRUCCIÓN:
//  ─────────────────────────────────────────────────────────
//  avanzar_vlts(N) → N ≠ 0  (N > 0 adelante, N < 0 atrás)
//  avanzar_ctms(N) → N ≠ 0
//  avanzar_mts(N)  → N ≠ 0
//  girar(N)        → N ∈ {-1, 0, 1}
//  circulo(R)      → 10 ≤ R ≤ 200 (radio en cm)
//  cuadrado(L)     → 10 ≤ L ≤ 200 (lado en cm)
//  rotar(N)        → N ≠ 0
//  caminar(N)      → N ≠ 0
//  moonwalk(N)     → N ≠ 0
//
//  ERRORES SEMÁNTICOS DETECTADOS:
//  ─────────────────────────────────────────────────────────
//  SEM001 → girar() con valor fuera de {-1, 0, 1}
//  SEM002 → instrucción de movimiento con parámetro 0
//  SEM003 → circulo() con radio fuera de [10, 200]
//  SEM004 → cuadrado() con lado fuera de [10, 200]
//  SEM005 → parámetro no es un entero válido
// ============================================================

public class SemanticAnalyzer
{
    private readonly List<NodoInstruccion>    _instrucciones;
    private readonly List<string>             _errores   = new();
    private readonly List<InstruccionValidada> _validadas = new();

    public SemanticAnalyzer(List<NodoInstruccion> instrucciones)
    {
        _instrucciones = instrucciones;
    }

    // ── ANALIZAR ─────────────────────────────────────────────
    public (List<InstruccionValidada> instrucciones, List<string> errores) Analyze()
    {
        int orden = 1;
        foreach (var nodo in _instrucciones)
        {
            var validada = nodo.EsCombinada
                ? ValidarCombinada(nodo, orden)
                : ValidarSimple(nodo, orden);

            if (validada != null)
            {
                _validadas.Add(validada);
                orden++;
            }
        }
        return (_validadas, _errores);
    }

    // ── VALIDAR INSTRUCCIÓN SIMPLE ───────────────────────────
    private InstruccionValidada? ValidarSimple(NodoInstruccion nodo, int orden)
    {
        // Verificar que el parámetro sea un entero válido
        if (!int.TryParse(nodo.Parametro, out int valor))
        {
            _errores.Add(
                $"[SEM005] Error semántico en línea {nodo.Linea}: " +
                $"el parámetro '{nodo.Parametro}' de '{nodo.Nombre}' no es un entero válido. " +
                $"Ejemplo correcto: {nodo.Nombre}(5)");
            return null;
        }

        // Validar según las reglas semánticas de cada instrucción
        if (!ValidarParametro(nodo.Nombre, valor, nodo.Linea))
            return null;

        // Construir instrucción validada
        var validada = new InstruccionValidada
        {
            Nombre = nodo.Nombre,
            Raw    = nodo.Raw,
            Orden  = orden,
            Linea  = nodo.Linea
        };

        switch (nodo.Nombre)
        {
            case "circulo":  validada.ParametroR = valor; break;
            case "cuadrado": validada.ParametroL = valor; break;
            default:         validada.ParametroN = valor; break;
        }

        return validada;
    }

    // ── VALIDAR INSTRUCCIÓN COMBINADA ─────────────────────────
    private InstruccionValidada? ValidarCombinada(NodoInstruccion nodo, int orden)
    {
        bool todo_valido = true;

        foreach (var parte in nodo.Partes)
        {
            var idx = parte.IndexOf('(');
            if (idx < 0) continue;

            var nombre = parte[..idx].Trim();
            var param  = parte[(idx + 1)..parte.LastIndexOf(')')].Trim();

            if (!int.TryParse(param, out int v))
            {
                _errores.Add(
                    $"[SEM005] Error semántico en línea {nodo.Linea}: " +
                    $"parámetro inválido en la parte combinada '{parte}'. " +
                    "Se esperaba un entero.");
                todo_valido = false;
                continue;
            }

            if (!ValidarParametro(nombre, v, nodo.Linea))
                todo_valido = false;
        }

        if (!todo_valido) return null;

        return new InstruccionValidada
        {
            Nombre      = "combinada",
            Raw         = nodo.Raw,
            Orden       = orden,
            Linea       = nodo.Linea,
            EsCombinada = true
        };
    }

    // ── VALIDAR PARÁMETRO POR INSTRUCCIÓN ────────────────────
    private bool ValidarParametro(string nombre, int valor, int linea)
    {
        switch (nombre)
        {
            case "avanzar_vlts":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'avanzar_vlts' no acepta 0. " +
                        "Use un valor positivo para avanzar o negativo para retroceder. " +
                        "Ejemplo: avanzar_vlts(3) o avanzar_vlts(-2)");
                    return false;
                }
                return true;

            case "avanzar_ctms":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'avanzar_ctms' no acepta 0. " +
                        "Use un valor positivo para avanzar o negativo para retroceder. " +
                        "Ejemplo: avanzar_ctms(50) o avanzar_ctms(-30)");
                    return false;
                }
                return true;

            case "avanzar_mts":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'avanzar_mts' no acepta 0. " +
                        "Use un valor positivo para avanzar o negativo para retroceder. " +
                        "Ejemplo: avanzar_mts(2) o avanzar_mts(-1)");
                    return false;
                }
                return true;

            case "girar":
                if (valor != -1 && valor != 0 && valor != 1)
                {
                    _errores.Add(
                        $"[SEM001] Error semántico en línea {linea}: " +
                        $"'girar()' solo acepta los valores: " +
                        $"1 (girar derecha), -1 (girar izquierda), 0 (avanzar recto). " +
                        $"Se recibió {valor}.");
                    return false;
                }
                return true;

            case "circulo":
                if (valor < 10 || valor > 200)
                {
                    _errores.Add(
                        $"[SEM003] Error semántico en línea {linea}: " +
                        $"'circulo()' requiere un radio entre 10 y 200 cm. " +
                        $"Se recibió {valor} cm. " +
                        "Ejemplo válido: circulo(50)");
                    return false;
                }
                return true;

            case "cuadrado":
                if (valor < 10 || valor > 200)
                {
                    _errores.Add(
                        $"[SEM004] Error semántico en línea {linea}: " +
                        $"'cuadrado()' requiere un lado entre 10 y 200 cm. " +
                        $"Se recibió {valor} cm. " +
                        "Ejemplo válido: cuadrado(30)");
                    return false;
                }
                return true;

            case "rotar":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'rotar' no acepta 0. " +
                        "Use un valor positivo o negativo. " +
                        "Ejemplo: rotar(2)");
                    return false;
                }
                return true;

            case "caminar":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'caminar' no acepta 0. " +
                        "Use un valor positivo o negativo. " +
                        "Ejemplo: caminar(5)");
                    return false;
                }
                return true;

            case "moonwalk":
                if (valor == 0)
                {
                    _errores.Add(
                        $"[SEM002] Error semántico en línea {linea}: " +
                        $"'moonwalk' no acepta 0. " +
                        "Use un valor positivo o negativo. " +
                        "Ejemplo: moonwalk(3)");
                    return false;
                }
                return true;

            default:
                return true;
        }
    }
}

// ── INSTRUCCIÓN VALIDADA ─────────────────────────────────────
/// <summary>
/// Representa una instrucción que pasó todas las fases del
/// compilador: léxico, sintáctico y semántico. Lista para
/// ser transpilada al lenguaje destino.
/// </summary>
public class InstruccionValidada
{
    public string Nombre      { get; set; } = string.Empty;
    public string Raw         { get; set; } = string.Empty;
    public int    Orden       { get; set; }
    public int    Linea       { get; set; }
    public bool   EsCombinada { get; set; } = false;
    public int?   ParametroN  { get; set; }  // avanzar, girar, rotar, caminar, moonwalk
    public int?   ParametroR  { get; set; }  // circulo (radio)
    public int?   ParametroL  { get; set; }  // cuadrado (lado)
}