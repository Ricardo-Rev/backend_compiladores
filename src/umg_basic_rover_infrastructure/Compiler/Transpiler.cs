namespace umg_basic_rover_infrastructure.Compiler;

public class Transpiler
{
    public string TranspilarAPython(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("import rover_sdk");
        sb.AppendLine();
        sb.AppendLine($"def {nombre}():");
        if (!instrucciones.Any()) sb.AppendLine("    pass");
        else foreach (var i in instrucciones.OrderBy(x => x.Orden)) sb.AppendLine($"    {LineaPython(i)}");
        sb.AppendLine();
        sb.AppendLine($"if __name__ == '__main__':");
        sb.AppendLine($"    {nombre}()");
        return sb.ToString();
    }

    public string TranspilarACSharp(string nombre, List<InstruccionValidada> instrucciones)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// Programa: {nombre} | Generado por UMG Basic Rover 2.0 Compiler");
        sb.AppendLine("using RoverSDK;");
        sb.AppendLine($"public class {nombre} {{");
        sb.AppendLine("    public static void Main(string[] args) {");
        sb.AppendLine("        var rover = new RoverController();");
        foreach (var i in instrucciones.OrderBy(x => x.Orden)) sb.AppendLine($"        {LineaCSharp(i)}");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

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
}
