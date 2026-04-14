namespace umg_basic_rover_infrastructure.Compiler;

public class Transpiler
{
    // ── PYTHON ───────────────────────────────────────────────
    public string TranspilarAPython(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("import rover_sdk");
        sb.AppendLine();
        sb.AppendLine($"def {nombre}():");
        if (!instrucciones.Any()) sb.AppendLine("    pass");
        else foreach (var i in instrucciones.OrderBy(x => x.Orden))
            sb.AppendLine($"    {LineaPython(i)}");
        sb.AppendLine();
        sb.AppendLine($"if __name__ == '__main__':");
        sb.AppendLine($"    {nombre}()");
        return sb.ToString();
    }

    // ── C# ───────────────────────────────────────────────────
    public string TranspilarACSharp(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler");
        sb.AppendLine("using RoverSDK;");
        sb.AppendLine($"public class {nombre} {{");
        sb.AppendLine("    public static void Main(string[] args) {");
        sb.AppendLine("        var rover = new RoverController();");
        foreach (var i in instrucciones.OrderBy(x => x.Orden))
            sb.AppendLine($"        {LineaCSharp(i)}");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── JAVA ─────────────────────────────────────────────────
    public string TranspilarAJava(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler");
        sb.AppendLine("import rover.RoverSDK;");
        sb.AppendLine();
        sb.AppendLine($"public class {nombre} {{");
        sb.AppendLine("    public static void main(String[] args) {");
        sb.AppendLine("        RoverSDK rover = new RoverSDK();");
        foreach (var i in instrucciones.OrderBy(x => x.Orden))
            sb.AppendLine($"        {LineaJava(i)}");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── C++ ──────────────────────────────────────────────────
    public string TranspilarACpp(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler");
        sb.AppendLine("#include \"rover_sdk.h\"");
        sb.AppendLine();
        sb.AppendLine($"void {nombre}() {{");
        foreach (var i in instrucciones.OrderBy(x => x.Orden))
            sb.AppendLine($"    {LineaCpp(i)}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("int main() {");
        sb.AppendLine($"    {nombre}();");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── LÍNEAS PYTHON ────────────────────────────────────────
    private string LineaPython(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover_sdk.avanzar_vueltas({i.ParametroN})  # {i.Raw}",
        "avanzar_ctms" => $"rover_sdk.avanzar_centimetros({i.ParametroN})  # {i.Raw}",
        "avanzar_mts"  => $"rover_sdk.avanzar_metros({i.ParametroN})  # {i.Raw}",
        "girar"        => GirarPython(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover_sdk.dibujar_circulo({i.ParametroR})  # {i.Raw}",
        "cuadrado"     => $"rover_sdk.dibujar_cuadrado({i.ParametroL})  # {i.Raw}",
        "rotar"        => $"rover_sdk.rotar({i.ParametroN})  # {i.Raw}",
        "caminar"      => $"rover_sdk.caminar({i.ParametroN})  # {i.Raw}",
        "moonwalk"     => $"rover_sdk.moonwalk({i.ParametroN})  # {i.Raw}",
        "combinada"    => $"rover_sdk.ejecutar_combinada('{i.Raw}')",
        _              => $"# desconocido: {i.Raw}"
    };

    // ── LÍNEAS C# ────────────────────────────────────────────
    private string LineaCSharp(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover.AvanzarVueltas({i.ParametroN}); // {i.Raw}",
        "avanzar_ctms" => $"rover.AvanzarCentimetros({i.ParametroN}); // {i.Raw}",
        "avanzar_mts"  => $"rover.AvanzarMetros({i.ParametroN}); // {i.Raw}",
        "girar"        => GirarCSharp(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover.DibujarCirculo({i.ParametroR}); // {i.Raw}",
        "cuadrado"     => $"rover.DibujarCuadrado({i.ParametroL}); // {i.Raw}",
        "rotar"        => $"rover.Rotar({i.ParametroN}); // {i.Raw}",
        "caminar"      => $"rover.Caminar({i.ParametroN}); // {i.Raw}",
        "moonwalk"     => $"rover.Moonwalk({i.ParametroN}); // {i.Raw}",
        "combinada"    => $"rover.EjecutarCombinada(\"{i.Raw}\"); // combinada",
        _              => $"// desconocido: {i.Raw}"
    };

    // ── LÍNEAS JAVA ──────────────────────────────────────────
    private string LineaJava(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover.avanzarVueltas({i.ParametroN}); // {i.Raw}",
        "avanzar_ctms" => $"rover.avanzarCentimetros({i.ParametroN}); // {i.Raw}",
        "avanzar_mts"  => $"rover.avanzarMetros({i.ParametroN}); // {i.Raw}",
        "girar"        => GirarJava(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover.dibujarCirculo({i.ParametroR}); // {i.Raw}",
        "cuadrado"     => $"rover.dibujarCuadrado({i.ParametroL}); // {i.Raw}",
        "rotar"        => $"rover.rotar({i.ParametroN}); // {i.Raw}",
        "caminar"      => $"rover.caminar({i.ParametroN}); // {i.Raw}",
        "moonwalk"     => $"rover.moonwalk({i.ParametroN}); // {i.Raw}",
        "combinada"    => $"rover.ejecutarCombinada(\"{i.Raw}\"); // combinada",
        _              => $"// desconocido: {i.Raw}"
    };

    // ── LÍNEAS C++ ───────────────────────────────────────────
    private string LineaCpp(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"rover::avanzar_vueltas({i.ParametroN}); // {i.Raw}",
        "avanzar_ctms" => $"rover::avanzar_centimetros({i.ParametroN}); // {i.Raw}",
        "avanzar_mts"  => $"rover::avanzar_metros({i.ParametroN}); // {i.Raw}",
        "girar"        => GirarCpp(i.ParametroN ?? 0, i.Raw),
        "circulo"      => $"rover::dibujar_circulo({i.ParametroR}); // {i.Raw}",
        "cuadrado"     => $"rover::dibujar_cuadrado({i.ParametroL}); // {i.Raw}",
        "rotar"        => $"rover::rotar({i.ParametroN}); // {i.Raw}",
        "caminar"      => $"rover::caminar({i.ParametroN}); // {i.Raw}",
        "moonwalk"     => $"rover::moonwalk({i.ParametroN}); // {i.Raw}",
        "combinada"    => $"rover::ejecutar_combinada(\"{i.Raw}\"); // combinada",
        _              => $"// desconocido: {i.Raw}"
    };

    // ── HELPERS GIRAR ────────────────────────────────────────
    private string GirarPython(int n, string raw) => n switch
    {
        1  => $"rover_sdk.activar_motor_izquierdo()  # {raw} → derecha",
        -1 => $"rover_sdk.activar_motor_derecho()    # {raw} → izquierda",
        _  => $"rover_sdk.activar_ambos_motores()    # {raw} → recto"
    };

    private string GirarCSharp(int n, string raw) => n switch
    {
        1  => $"rover.ActivarMotorIzquierdo(); // {raw} → derecha",
        -1 => $"rover.ActivarMotorDerecho();   // {raw} → izquierda",
        _  => $"rover.ActivarAmbosMotores();   // {raw} → recto"
    };

    private string GirarJava(int n, string raw) => n switch
    {
        1  => $"rover.activarMotorIzquierdo(); // {raw} → derecha",
        -1 => $"rover.activarMotorDerecho();   // {raw} → izquierda",
        _  => $"rover.activarAmbosMotores();   // {raw} → recto"
    };

    private string GirarCpp(int n, string raw) => n switch
    {
        1  => $"rover::activar_motor_izquierdo(); // {raw} → derecha",
        -1 => $"rover::activar_motor_derecho();   // {raw} → izquierda",
        _  => $"rover::activar_ambos_motores();   // {raw} → recto"
    };

    // ── ARDUINO (SERIAL) ─────────────────────────────────────
    // Genera comandos seriales listos para enviar desde
    // la Raspberry Pi al Arduino UNO por USB (9600 baud).
    // Formato esperado por procesarComando() en el firmware.
    public string TranspilarAArduino(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Programa: {nombre} | UMG Basic Rover 2.0 | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Enviar cada línea por Serial USB a 9600 baud al Arduino UNO");
        sb.AppendLine($"# Esperar respuesta ACK: antes de enviar el siguiente comando");
        sb.AppendLine();
        foreach (var i in instrucciones.OrderBy(x => x.Orden))
            sb.AppendLine(LineaArduino(i));
        return sb.ToString();
    }

    // ── LÍNEAS ARDUINO ───────────────────────────────────────
    private string LineaArduino(InstruccionValidada i) => i.Nombre switch
    {
        "avanzar_vlts" => $"AV_VLT:{i.ParametroN}",
        "avanzar_ctms" => $"AV_CM:{i.ParametroN}",
        "avanzar_mts"  => $"AV_MTS:{i.ParametroN}",
        "girar"        => $"GR:{i.ParametroN ?? 0}",
        "circulo"      => $"CIR:{i.ParametroR}",
        "cuadrado"     => $"CUA:{i.ParametroL}",
        "rotar"        => $"ROT:{i.ParametroN}",
        "caminar"      => $"CAM:{i.ParametroN}",
        "moonwalk"     => $"MWK:{i.ParametroN}",
        "combinada"    => LineaArduinoCombinada(i.Raw),
        _              => $"# desconocido: {i.Raw}"
    };

    // Instrucción combinada: girar(1) + avanzar_mts(3)
    // Se separa en dos comandos seriales consecutivos
    private string LineaArduinoCombinada(string raw)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# combinada: {raw}");

        // Extraer cada parte separada por '+'
        var partes = raw.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var parte in partes)
        {
            var p = parte.Trim().TrimEnd(';');
            if (p.StartsWith("girar("))
            {
                var n = ExtraerParametro(p);
                sb.AppendLine($"GR:{n}");
            }
            else if (p.StartsWith("avanzar_vlts("))
            {
                var n = ExtraerParametro(p);
                sb.AppendLine($"AV_VLT:{n}");
            }
            else if (p.StartsWith("avanzar_ctms(") || p.StartsWith("avanzar_cms("))
            {
                var n = ExtraerParametro(p);
                sb.AppendLine($"AV_CM:{n}");
            }
            else if (p.StartsWith("avanzar_mts("))
            {
                var n = ExtraerParametro(p);
                sb.AppendLine($"AV_MTS:{n}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string ExtraerParametro(string instruccion)
    {
        var ini = instruccion.IndexOf('(');
        var fin = instruccion.IndexOf(')');
        if (ini < 0 || fin < 0) return "0";
        return instruccion.Substring(ini + 1, fin - ini - 1).Trim();
    }
}