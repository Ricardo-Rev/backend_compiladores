namespace umg_basic_rover_infrastructure.Compiler;

public class SemanticAnalyzer
{
    private readonly List<NodoInstruccion> _instrucciones;
    private readonly List<string> _errores = new();
    private readonly List<InstruccionValidada> _validadas = new();

    public SemanticAnalyzer(List<NodoInstruccion> instrucciones) { _instrucciones = instrucciones; }

    public (List<InstruccionValidada> instrucciones, List<string> errores) Analyze()
    {
        int orden = 1;
        foreach (var nodo in _instrucciones)
        {
            var validada = nodo.EsCombinada ? ValidarCombinada(nodo, orden) : ValidarSimple(nodo, orden);
            if (validada != null) { _validadas.Add(validada); orden++; }
        }
        return (_validadas, _errores);
    }

    private InstruccionValidada? ValidarSimple(NodoInstruccion nodo, int orden)
    {
        if (!int.TryParse(nodo.Parametro, out int valor))
        {
            _errores.Add($"Error semántico en línea {nodo.Linea}: el parámetro '{nodo.Parametro}' de '{nodo.Nombre}' no es un entero válido.");
            return null;
        }
        if (!ValidarParametro(nodo.Nombre, valor, nodo.Linea)) return null;

        var v = new InstruccionValidada { Nombre = nodo.Nombre, Raw = nodo.Raw, Orden = orden, Linea = nodo.Linea };
        switch (nodo.Nombre)
        {
            case "circulo":  v.ParametroR = valor; break;
            case "cuadrado": v.ParametroL = valor; break;
            default:         v.ParametroN = valor; break;
        }
        return v;
    }

    private InstruccionValidada? ValidarCombinada(NodoInstruccion nodo, int orden)
    {
        bool ok = true;
        foreach (var parte in nodo.Partes)
        {
            var idx = parte.IndexOf('(');
            if (idx < 0) continue;
            var nombre = parte[..idx].Trim();
            var param  = parte[(idx + 1)..parte.LastIndexOf(')')].Trim();
            if (!int.TryParse(param, out int v)) { _errores.Add($"Error semántico en línea {nodo.Linea}: parámetro inválido en '{parte}'."); ok = false; continue; }
            if (!ValidarParametro(nombre, v, nodo.Linea)) ok = false;
        }
        if (!ok) return null;
        return new InstruccionValidada { Nombre = "combinada", Raw = nodo.Raw, Orden = orden, Linea = nodo.Linea, EsCombinada = true };
    }

    private bool ValidarParametro(string nombre, int valor, int linea)
    {
        switch (nombre)
        {
            case "avanzar_vlts": case "avanzar_ctms": case "avanzar_mts": case "rotar": case "caminar": case "moonwalk":
                if (valor == 0) { _errores.Add($"Error semántico en línea {linea}: '{nombre}' no acepta 0. Use positivo (adelante) o negativo (atrás)."); return false; }
                return true;
            case "girar":
                if (valor != -1 && valor != 0 && valor != 1) { _errores.Add($"Error semántico en línea {linea}: 'girar()' solo acepta -1, 0 o 1. Se recibió {valor}."); return false; }
                return true;
            case "circulo":
                if (valor < 10 || valor > 200) { _errores.Add($"Error semántico en línea {linea}: 'circulo()' requiere radio entre 10 y 200 cm. Se recibió {valor}."); return false; }
                return true;
            case "cuadrado":
                if (valor < 10 || valor > 200) { _errores.Add($"Error semántico en línea {linea}: 'cuadrado()' requiere lado entre 10 y 200 cm. Se recibió {valor}."); return false; }
                return true;
            default:
                return true;
        }
    }
}

public class InstruccionValidada
{
    public string Nombre { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int Linea { get; set; }
    public bool EsCombinada { get; set; } = false;
    public int? ParametroN { get; set; }
    public int? ParametroR { get; set; }
    public int? ParametroL { get; set; }
}
