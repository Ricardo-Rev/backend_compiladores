namespace umg_basic_rover_infrastructure.Compiler;

// ============================================================
//  Transpiler.cs — UMG Basic Rover 2.0
//
//  Convierte las instrucciones validadas del compilador UMG++
//  a código ejecutable en múltiples lenguajes destino:
//
//  ✅ Python   → rover_sdk (target por defecto)
//  ✅ C#       → RoverSDK
//  ✅ Java     → RoverController
//  ✅ C++      → rover_sdk.h
//
//  Cada método Transpilara[Lenguaje] recibe:
//    - nombre:        nombre del programa extraído del token IDENTIFIER
//    - instrucciones: lista validada por el SemanticAnalyzer
//
//  Retorna el código fuente completo como string listo para
//  mostrar en el editor o guardar como archivo.
// ============================================================

public class Transpiler
{
    // ── PYTHON ───────────────────────────────────────────────
    /// <summary>
    /// Transpila a Python usando el módulo rover_sdk.
    /// Genera una función con el nombre del programa y un
    /// bloque __main__ para ejecución directa.
    /// </summary>
    public string TranspilarAPython(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# ============================================================");
        sb.AppendLine($"# Programa  : {nombre}");
        sb.AppendLine($"# Generado  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Compilador: UMG Basic Rover 2.0 Compiler");
        sb.AppendLine($"# Lenguaje  : Python 3");
        sb.AppendLine($"# ============================================================");
        sb.AppendLine("import rover_sdk");
        sb.AppendLine();
        sb.AppendLine($"def {nombre}():");
        sb.AppendLine($"    \"\"\"Programa UMG++ transpilado a Python.\"\"\"");

        if (!instrucciones.Any())
        {
            sb.AppendLine("    pass  # programa vacío");
        }
        else
        {
            foreach (var inst in instrucciones.OrderBy(x => x.Orden))
            {
                sb.AppendLine($"    {LineaPython(inst)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("if __name__ == '__main__':");
        sb.AppendLine($"    {nombre}()");
        return sb.ToString();
    }

    // ── C# ───────────────────────────────────────────────────
    /// <summary>
    /// Transpila a C# usando la clase RoverController del SDK.
    /// Genera una clase estática con método Main ejecutable.
    /// </summary>
    public string TranspilarACSharp(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// ============================================================");
        sb.AppendLine($"// Programa  : {nombre}");
        sb.AppendLine($"// Generado  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Compilador: UMG Basic Rover 2.0 Compiler");
        sb.AppendLine($"// Lenguaje  : C# (.NET 8)");
        sb.AppendLine($"// ============================================================");
        sb.AppendLine("using RoverSDK;");
        sb.AppendLine();
        sb.AppendLine($"public static class {NombreValido(nombre)}");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Main(string[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        var rover = new RoverController();");
        sb.AppendLine();

        foreach (var inst in instrucciones.OrderBy(x => x.Orden))
        {
            sb.AppendLine($"        {LineaCSharp(inst)}");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── JAVA ─────────────────────────────────────────────────
    /// <summary>
    /// Transpila a Java usando la clase RoverController del SDK.
    /// Genera una clase pública con método main estándar de Java.
    /// </summary>
    public string TranspilarAJava(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// ============================================================");
        sb.AppendLine($"// Programa  : {nombre}");
        sb.AppendLine($"// Generado  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Compilador: UMG Basic Rover 2.0 Compiler");
        sb.AppendLine($"// Lenguaje  : Java 17");
        sb.AppendLine($"// ============================================================");
        sb.AppendLine("import rover.RoverController;");
        sb.AppendLine();
        sb.AppendLine($"public class {NombreValido(nombre)} {{");
        sb.AppendLine();
        sb.AppendLine("    public static void main(String[] args) {");
        sb.AppendLine("        RoverController rover = new RoverController();");
        sb.AppendLine();

        foreach (var inst in instrucciones.OrderBy(x => x.Orden))
        {
            sb.AppendLine($"        {LineaJava(inst)}");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── C++ ──────────────────────────────────────────────────
    /// <summary>
    /// Transpila a C++ usando la librería rover_sdk.h.
    /// Genera un archivo .cpp con función main y uso del SDK.
    /// </summary>
    public string TranspilarACpp(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// ============================================================");
        sb.AppendLine($"// Programa  : {nombre}");
        sb.AppendLine($"// Generado  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Compilador: UMG Basic Rover 2.0 Compiler");
        sb.AppendLine($"// Lenguaje  : C++17");
        sb.AppendLine($"// ============================================================");
        sb.AppendLine("#include <iostream>");
        sb.AppendLine("#include \"rover_sdk.h\"");
        sb.AppendLine();
        sb.AppendLine("int main() {");
        sb.AppendLine("    RoverController rover;");
        sb.AppendLine();

        foreach (var inst in instrucciones.OrderBy(x => x.Orden))
        {
            sb.AppendLine($"    {LineaCpp(inst)}");
        }

        sb.AppendLine();
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── LÍNEAS PYTHON ─────────────────────────────────────────
    private string LineaPython(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover_sdk.avanzar_vueltas({i.ParametroN})         # {i.Raw}",
        "avanzar_ctms" => $"rover_sdk.avanzar_centimetros({i.ParametroN})     # {i.Raw}",
        "avanzar_mts"  => $"rover_sdk.avanzar_metros({i.ParametroN})          # {i.Raw}",
        "girar"        => GirarPython(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover_sdk.dibujar_circulo({i.ParametroR})         # {i.Raw}",
        "cuadrado"     => $"rover_sdk.dibujar_cuadrado({i.ParametroL})        # {i.Raw}",
        "rotar"        => $"rover_sdk.rotar({i.ParametroN})                   # {i.Raw}",
        "caminar"      => $"rover_sdk.caminar({i.ParametroN})                 # {i.Raw}",
        "moonwalk"     => $"rover_sdk.moonwalk({i.ParametroN})                # {i.Raw}",
        "combinada"    => $"rover_sdk.ejecutar_combinada('{i.Raw}')           # combinada",
        _              => $"# instrucción desconocida: {i.Raw}"
    };

    // ── LÍNEAS C# ─────────────────────────────────────────────
    private string LineaCSharp(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover.AvanzarVueltas({i.ParametroN});         // {i.Raw}",
        "avanzar_ctms" => $"rover.AvanzarCentimetros({i.ParametroN});     // {i.Raw}",
        "avanzar_mts"  => $"rover.AvanzarMetros({i.ParametroN});          // {i.Raw}",
        "girar"        => GirarCSharp(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover.DibujarCirculo({i.ParametroR});         // {i.Raw}",
        "cuadrado"     => $"rover.DibujarCuadrado({i.ParametroL});        // {i.Raw}",
        "rotar"        => $"rover.Rotar({i.ParametroN});                  // {i.Raw}",
        "caminar"      => $"rover.Caminar({i.ParametroN});                // {i.Raw}",
        "moonwalk"     => $"rover.Moonwalk({i.ParametroN});               // {i.Raw}",
        "combinada"    => $"rover.EjecutarCombinada(\"{i.Raw}\");         // combinada",
        _              => $"// instrucción desconocida: {i.Raw}"
    };

    // ── LÍNEAS JAVA ───────────────────────────────────────────
    private string LineaJava(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover.avanzarVueltas({i.ParametroN});         // {i.Raw}",
        "avanzar_ctms" => $"rover.avanzarCentimetros({i.ParametroN});     // {i.Raw}",
        "avanzar_mts"  => $"rover.avanzarMetros({i.ParametroN});          // {i.Raw}",
        "girar"        => GirarJava(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover.dibujarCirculo({i.ParametroR});         // {i.Raw}",
        "cuadrado"     => $"rover.dibujarCuadrado({i.ParametroL});        // {i.Raw}",
        "rotar"        => $"rover.rotar({i.ParametroN});                  // {i.Raw}",
        "caminar"      => $"rover.caminar({i.ParametroN});                // {i.Raw}",
        "moonwalk"     => $"rover.moonwalk({i.ParametroN});               // {i.Raw}",
        "combinada"    => $"rover.ejecutarCombinada(\"{i.Raw}\");         // combinada",
        _              => $"// instrucción desconocida: {i.Raw}"
    };

    // ── LÍNEAS C++ ────────────────────────────────────────────
    private string LineaCpp(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover.avanzarVueltas({i.ParametroN});         // {i.Raw}",
        "avanzar_ctms" => $"rover.avanzarCentimetros({i.ParametroN});     // {i.Raw}",
        "avanzar_mts"  => $"rover.avanzarMetros({i.ParametroN});          // {i.Raw}",
        "girar"        => GirarCpp(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover.dibujarCirculo({i.ParametroR});         // {i.Raw}",
        "cuadrado"     => $"rover.dibujarCuadrado({i.ParametroL});        // {i.Raw}",
        "rotar"        => $"rover.rotar({i.ParametroN});                  // {i.Raw}",
        "caminar"      => $"rover.caminar({i.ParametroN});                // {i.Raw}",
        "moonwalk"     => $"rover.moonwalk({i.ParametroN});               // {i.Raw}",
        "combinada"    => $"rover.ejecutarCombinada(\"{i.Raw}\");         // combinada",
        _              => $"// instrucción desconocida: {i.Raw}"
    };

    // ── GIRAR POR LENGUAJE ────────────────────────────────────
    private string GirarPython(int n, string raw) => n switch
    {
        1  => $"rover_sdk.activar_motor_izquierdo()  # {raw} → gira a la derecha",
        -1 => $"rover_sdk.activar_motor_derecho()    # {raw} → gira a la izquierda",
        _  => $"rover_sdk.activar_ambos_motores()    # {raw} → avanza recto"
    };

    private string GirarCSharp(int n, string raw) => n switch
    {
        1  => $"rover.ActivarMotorIzquierdo();  // {raw} → gira a la derecha",
        -1 => $"rover.ActivarMotorDerecho();    // {raw} → gira a la izquierda",
        _  => $"rover.ActivarAmbosMotores();    // {raw} → avanza recto"
    };

    private string GirarJava(int n, string raw) => n switch
    {
        1  => $"rover.activarMotorIzquierdo();  // {raw} → gira a la derecha",
        -1 => $"rover.activarMotorDerecho();    // {raw} → gira a la izquierda",
        _  => $"rover.activarAmbosMotores();    // {raw} → avanza recto"
    };

    private string GirarCpp(int n, string raw) => n switch
    {
        1  => $"rover.activarMotorIzquierdo();  // {raw} → gira a la derecha",
        -1 => $"rover.activarMotorDerecho();    // {raw} → gira a la izquierda",
        _  => $"rover.activarAmbosMotores();    // {raw} → avanza recto"
    };

    // ── UTILIDADES ────────────────────────────────────────────
    /// <summary>
    /// Asegura que el nombre del programa sea un identificador
    /// válido para todos los lenguajes destino.
    /// </summary>
    private static string NombreValido(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "Programa";
        var limpio = new System.Text.StringBuilder();
        foreach (var c in nombre)
        {
            if (char.IsLetterOrDigit(c) || c == '_') limpio.Append(c);
            else limpio.Append('_');
        }
        if (char.IsDigit(limpio[0])) limpio.Insert(0, '_');
        return limpio.ToString();
    }
}