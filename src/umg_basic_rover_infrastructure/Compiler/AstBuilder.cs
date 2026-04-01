namespace umg_basic_rover_infrastructure.Compiler;

public class AstBuilder
{
    public AstNodo Construir(List<Token> tokens, List<InstruccionValidada> instrucciones)
    {
        var raiz = new AstNodo
        {
            tipo  = "PROGRAMA",
            valor = ExtraerNombre(tokens),
            linea = 1
        };

        var bloque = new AstNodo
        {
            tipo  = "BLOQUE",
            valor = "BEGIN...END",
            linea = ExtraerLineaBegin(tokens)
        };

        foreach (var inst in instrucciones.OrderBy(i => i.Orden))
            bloque.hijos.Add(inst.EsCombinada ? ConstruirCombinada(inst) : ConstruirSimple(inst));

        raiz.hijos.Add(bloque);
        return raiz;
    }

    private AstNodo ConstruirSimple(InstruccionValidada inst)
    {
        var nodo = new AstNodo
        {
            tipo  = "INSTRUCCION",
            valor = inst.Nombre,
            linea = inst.Linea
        };

        // Agregar el parámetro como hijo según el tipo de instrucción
        var param = inst.ParametroN?.ToString()
                 ?? inst.ParametroR?.ToString()
                 ?? inst.ParametroL?.ToString();

        if (param != null)
            nodo.hijos.Add(new AstNodo { tipo = "PARAMETRO", valor = param, linea = inst.Linea });

        return nodo;
    }

    private AstNodo ConstruirCombinada(InstruccionValidada inst)
    {
        var nodo = new AstNodo
        {
            tipo  = "INSTRUCCION_COMBINADA",
            valor = inst.Raw,
            linea = inst.Linea
        };

        // Parsear las partes del raw: "girar(1) + avanzar_mts(3)"
        var partes = inst.Raw.Split('+');
        foreach (var parte in partes)
        {
            var p    = parte.Trim();
            var idx  = p.IndexOf('(');
            if (idx < 0) continue;

            var nombre = p[..idx].Trim();
            var param  = p[(idx + 1)..p.LastIndexOf(')')].Trim();

            nodo.hijos.Add(new AstNodo
            {
                tipo  = "COMPONENTE",
                valor = nombre,
                linea = inst.Linea,
                hijos = new List<AstNodo>
                {
                    new() { tipo = "PARAMETRO", valor = param, linea = inst.Linea }
                }
            });
        }

        return nodo;
    }

    private string ExtraerNombre(List<Token> tokens)
    {
        var idx = tokens.FindIndex(t => t.Tipo == TokenType.KEYWORD && t.Lexema == "PROGRAM");
        return (idx >= 0 && idx + 1 < tokens.Count) ? tokens[idx + 1].Lexema : "programa";
    }

    private int ExtraerLineaBegin(List<Token> tokens)
        => tokens.FirstOrDefault(t => t.Tipo == TokenType.KEYWORD && t.Lexema == "BEGIN")?.Linea ?? 1;
}