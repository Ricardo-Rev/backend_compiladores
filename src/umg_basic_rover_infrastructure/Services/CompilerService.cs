using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.Compiler;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  CompilerService.cs — UMG Basic Rover 2.0
//
//  Orquesta el pipeline completo del compilador UMG++:
//
//  FASE 1 → Léxico      (Lexer.cs)
//  FASE 2 → Sintáctico  (Parser.cs)
//  FASE 3 → Semántico   (SemanticAnalyzer.cs)
//  FASE 4 → Transpilado (Transpiler.cs) → python | csharp | java | cpp
//  FASE 5 → Simulación  (GenerarSimulacion)
//  FASE 6 → Persistencia en Azure SQL Server
//
//  Cada fase detiene el pipeline si encuentra errores,
//  retornando el tipo de error específico con número de línea.
// ============================================================

public class CompilerService : ICompilerService
{
    private readonly rover_db_context _db;
    private readonly ILogger<CompilerService> _logger;

    // Lenguajes destino soportados
    private static readonly HashSet<string> LENGUAJES_VALIDOS = new(StringComparer.OrdinalIgnoreCase)
    {
        "python", "csharp", "java", "cpp"
    };

    public CompilerService(rover_db_context db, ILogger<CompilerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── COMPILAR ─────────────────────────────────────────────
    public async Task<CompileResponse> CompileAsync(CompileRequest request, int usuario_id, int sesion_id)
    {
        // Sanitizar inputs para logging seguro
        var safe_modo    = SanitizarLog(request.modo ?? "compilar");
        var safe_lenguaje = SanitizarLog(request.lenguaje_destino ?? "python");

        _logger.LogInformation("[COMPILER] Iniciando compilación. Usuario: {u} | Modo: {m} | Lenguaje: {l}",
            usuario_id, safe_modo, safe_lenguaje);

        var sw       = Stopwatch.StartNew();
        var response = new CompileResponse();

        // Validar lenguaje destino
        var lenguaje = request.lenguaje_destino?.ToLower() ?? "python";
        if (!LENGUAJES_VALIDOS.Contains(lenguaje))
        {
            sw.Stop();
            response.exitoso    = false;
            response.resultado  = "error_parametro";
            response.tiempo_ms  = (int)sw.ElapsedMilliseconds;
            response.errores    = new List<ErrorDto>
            {
                new() { tipo = "parametro", mensaje = $"Lenguaje destino '{lenguaje}' no soportado. Use: python, csharp, java, cpp." }
            };
            return response;
        }

        // ── FASE 1: LÉXICO ───────────────────────────────────
        _logger.LogDebug("[COMPILER:LEXICO] Iniciando análisis léxico.");
        var lexer = new Lexer(request.codigo_fuente ?? string.Empty);
        var (tokens, errores_lex) = lexer.Tokenize();

        response.tokens = tokens
            .Where(t => t.Tipo != TokenType.EOF)
            .Select(t => new TokenDto
            {
                linea   = t.Linea,
                columna = t.Columna,
                tipo    = t.Tipo.ToString(),
                lexema  = t.Lexema,
                valor   = t.Valor
            }).ToList();

        if (errores_lex.Any())
        {
            sw.Stop();
            _logger.LogWarning("[COMPILER:LEXICO] {n} error(es) léxicos encontrados.", errores_lex.Count);
            response.exitoso   = false;
            response.resultado = "error_lexico";
            response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores   = errores_lex.Select(e => new ErrorDto { tipo = "lexico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_lexico", tokens, errores_lex, new(), response.tiempo_ms);
            return response;
        }

        // ── FASE 2: SINTÁCTICO ───────────────────────────────
        _logger.LogDebug("[COMPILER:SINTACTICO] Iniciando análisis sintáctico.");
        var parser = new Parser(tokens);
        var (nodos, errores_sin) = parser.Parse();

        if (errores_sin.Any())
        {
            sw.Stop();
            _logger.LogWarning("[COMPILER:SINTACTICO] {n} error(es) sintácticos encontrados.", errores_sin.Count);
            response.exitoso   = false;
            response.resultado = "error_sintactico";
            response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores   = errores_sin.Select(e => new ErrorDto { tipo = "sintactico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_sintactico", tokens, errores_sin, new(), response.tiempo_ms);
            return response;
        }

        // ── FASE 3: SEMÁNTICO ────────────────────────────────
        _logger.LogDebug("[COMPILER:SEMANTICO] Iniciando análisis semántico.");
        var semantic = new SemanticAnalyzer(nodos);
        var (instrucciones, errores_sem) = semantic.Analyze();

        if (errores_sem.Any())
        {
            sw.Stop();
            _logger.LogWarning("[COMPILER:SEMANTICO] {n} error(es) semánticos encontrados.", errores_sem.Count);
            response.exitoso   = false;
            response.resultado = "error_semantico";
            response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores   = errores_sem.Select(e => new ErrorDto { tipo = "semantico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_semantico", tokens, errores_sem, new(), response.tiempo_ms);
            return response;
        }

        // ── FASE 4: TRANSPILACIÓN ────────────────────────────
        _logger.LogDebug("[COMPILER:TRANSPILER] Transpilando a '{l}'.", lenguaje);
        var transpiler = new Transpiler();
        var nombre     = ExtraerNombre(tokens);

        response.codigo_transpilado = lenguaje switch
        {
            "csharp" => transpiler.TranspilarACSharp(nombre, instrucciones),
            "java"   => transpiler.TranspilarAJava(nombre, instrucciones),
            "cpp"    => transpiler.TranspilarACpp(nombre, instrucciones),
            _        => transpiler.TranspilarAPython(nombre, instrucciones)  // python por defecto
        };

        // ── FASE 5: SIMULACIÓN ───────────────────────────────
        SimulacionDto? sim = null;
        if (request.modo is "compilar_simular" or "compilar_ejecutar")
        {
            _logger.LogDebug("[COMPILER:SIMULACION] Generando simulación de trayectoria.");
            sim = GenerarSimulacion(instrucciones);
        }

        // ── FASE 6: PERSISTENCIA ─────────────────────────────
        sw.Stop();
        response.tiempo_ms = (int)sw.ElapsedMilliseconds;

        var comp_id = await Persistir(request, usuario_id, sesion_id, "exito", tokens, new(), instrucciones, response.tiempo_ms, sim);

        response.exitoso        = true;
        response.resultado      = "exito";
        response.compilacion_id = comp_id;
        response.simulacion     = sim;
        response.instrucciones  = instrucciones
            .Select(i => new InstruccionDto
            {
                orden       = i.Orden,
                nombre      = i.Nombre,
                raw         = i.Raw,
                parametro_n = i.ParametroN,
                parametro_r = i.ParametroR,
                parametro_l = i.ParametroL
            }).ToList();

        _logger.LogInformation("[COMPILER] ✅ Compilación exitosa en {ms}ms. ID: {id} | Instrucciones: {n}",
            response.tiempo_ms, comp_id, instrucciones.Count);

        return response;
    }

    // ── HISTORIAL ─────────────────────────────────────────────
    public async Task<List<CompileHistoryResponse>> GetHistoryAsync(int usuario_id, int limite = 20)
    {
        _logger.LogInformation("[COMPILER:HISTORY] Consultando historial. Usuario: {u}", usuario_id);

        var historial = await _db.compilaciones
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_compilacion)
            .Take(limite)
            .Select(c => new CompileHistoryResponse
{
    id                  = c.id,
    resultado           = c.resultado ?? string.Empty,
    modo_compilacion    = c.modo_compilacion,
    tiempo_ms           = c.tiempo_compilacion_ms ?? 0,
    fecha_compilacion   = c.fecha_compilacion,
    total_instrucciones = _db.instrucciones_ejecutadas.Count(i => i.compilacion_id == c.id)
})
            .ToListAsync();

        return historial;
    }

    // ── PERSISTIR ─────────────────────────────────────────────
    private async Task<int> Persistir(
        CompileRequest req, int uid, int sid, string resultado,
        List<Token> tokens, List<string> errores,
        List<InstruccionValidada> instrucciones,
        int tiempo_ms, SimulacionDto? sim = null)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Guardar compilación
            var comp = new compilacion_entity
            {
                usuario_id            = uid,
                sesion_id             = sid,
                archivo_id            = req.archivo_id,
                codigo_fuente         = req.codigo_fuente,
                modo_compilacion      = req.modo,
                resultado             = resultado,
                tiempo_compilacion_ms = tiempo_ms,
                fecha_compilacion     = DateTime.Now
            };
            _db.compilaciones.Add(comp);
            await _db.SaveChangesAsync();

            // Guardar tokens del léxico
            _db.tokens_lexer.AddRange(
                tokens.Where(t => t.Tipo != TokenType.EOF)
                      .Select(t => new token_lexer_entity
                      {
                          compilacion_id = comp.id,
                          numero_linea   = t.Linea,
                          numero_columna = t.Columna,
                          tipo_token     = MapToken(t.Tipo),
                          lexema         = t.Lexema,
                          valor          = t.Valor
                      })
            );

            // Guardar errores si existen
            if (errores.Any())
            {
                var tipo_error = resultado.Replace("error_", string.Empty);
                _db.errores_compilacion.AddRange(
                    errores.Select(e => new error_compilacion_entity
                    {
                        compilacion_id = comp.id,
                        tipo_error     = tipo_error,
                        mensaje_error  = e
                    })
                );
            }

            // Guardar instrucciones ejecutadas
            if (instrucciones.Any())
            {
                var nombres  = instrucciones.Where(i => !i.EsCombinada).Select(i => i.Nombre).Distinct().ToList();
                var catalogo = await _db.instrucciones_umgpp
                    .Where(x => nombres.Contains(x.nombre_instruccion))
                    .ToDictionaryAsync(x => x.nombre_instruccion, x => x.id);

                foreach (var inst in instrucciones)
                {
                    var cat_id = inst.EsCombinada
                        ? (catalogo.Values.FirstOrDefault() == 0 ? 1 : catalogo.Values.First())
                        : (catalogo.TryGetValue(inst.Nombre, out var cid) ? cid : 1);

                    _db.instrucciones_ejecutadas.Add(new instruccion_ejecutada_entity
                    {
                        compilacion_id  = comp.id,
                        numero_orden    = inst.Orden,
                        instruccion_id  = cat_id,
                        parametro_n     = inst.ParametroN,
                        parametro_r     = inst.ParametroR,
                        parametro_l     = inst.ParametroL,
                        instruccion_raw = inst.Raw
                    });
                }
            }

            // Guardar simulación si existe
            if (sim != null)
            {
                var simulacion = new simulacion_entity
                {
                    compilacion_id        = comp.id,
                    usuario_id            = uid,
                    trayectoria_json      = sim.trayectoria_json,
                    duracion_estimada_seg = sim.duracion_estimada_seg,
                    distancia_total_cm    = sim.distancia_total_cm,
                    fecha_simulacion      = DateTime.Now
                };
                _db.simulaciones.Add(simulacion);
                await _db.SaveChangesAsync();
                sim.simulacion_id = simulacion.id;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return comp.id;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "[COMPILER] Error al persistir compilación. Usuario: {u}", uid);
            throw;
        }
    }

    // ── SIMULACIÓN ────────────────────────────────────────────
    /// <summary>
    /// Genera la trayectoria JSON del rover basada en las
    /// instrucciones validadas. Calcula distancia total y
    /// duración estimada en segundos.
    /// </summary>
    private SimulacionDto GenerarSimulacion(List<InstruccionValidada> instrucciones)
    {
        var puntos = new List<object>();
        decimal dist_total = 0;
        int     dur_total  = 0;

        foreach (var inst in instrucciones.OrderBy(x => x.Orden))
        {
            switch (inst.Nombre)
            {
                case "avanzar_vlts":
                    var cm_vlts = (inst.ParametroN ?? 0) * 20;
                    dist_total += Math.Abs(cm_vlts);
                    dur_total  += Math.Abs(inst.ParametroN ?? 0) * 2;
                    puntos.Add(new { tipo = "avanzar", valor = inst.ParametroN, unidad = "vueltas", cm = cm_vlts });
                    break;

                case "avanzar_ctms":
                    dist_total += Math.Abs(inst.ParametroN ?? 0);
                    dur_total  += (int)(Math.Abs(inst.ParametroN ?? 0) * 0.5);
                    puntos.Add(new { tipo = "avanzar", valor = inst.ParametroN, unidad = "centimetros", cm = inst.ParametroN });
                    break;

                case "avanzar_mts":
                    var cm_mts = (inst.ParametroN ?? 0) * 100;
                    dist_total += Math.Abs(cm_mts);
                    dur_total  += Math.Abs(inst.ParametroN ?? 0) * 5;
                    puntos.Add(new { tipo = "avanzar", valor = inst.ParametroN, unidad = "metros", cm = cm_mts });
                    break;

                case "girar":
                    dur_total += 2;
                    var direccion = (inst.ParametroN ?? 0) switch
                    {
                        1  => "derecha",
                        -1 => "izquierda",
                        _  => "recto"
                    };
                    puntos.Add(new { tipo = "girar", direccion });
                    break;

                case "circulo":
                    var circ = (decimal)(2 * Math.PI * (inst.ParametroR ?? 0));
                    dist_total += circ;
                    dur_total  += (inst.ParametroR ?? 0) / 10;
                    puntos.Add(new { tipo = "circulo", radio = inst.ParametroR });
                    break;

                case "cuadrado":
                    var lado = inst.ParametroL ?? 0;
                    dist_total += lado * 4;
                    dur_total  += lado / 5;
                    puntos.Add(new { tipo = "cuadrado", lado });
                    break;

                case "rotar":
                    dur_total += Math.Abs(inst.ParametroN ?? 0) * 3;
                    puntos.Add(new { tipo = "rotar", vueltas = inst.ParametroN });
                    break;

                case "caminar":
                    dur_total += Math.Abs(inst.ParametroN ?? 0) * 2;
                    puntos.Add(new { tipo = "caminar", pasos = inst.ParametroN });
                    break;

                case "moonwalk":
                    dur_total += Math.Abs(inst.ParametroN ?? 0) * 3;
                    puntos.Add(new { tipo = "moonwalk", pasos = inst.ParametroN });
                    break;

                case "combinada":
                    dur_total += 5;
                    puntos.Add(new { tipo = "combinada", raw = inst.Raw });
                    break;
            }
        }

        return new SimulacionDto
        {
            trayectoria_json      = JsonSerializer.Serialize(puntos),
            duracion_estimada_seg = dur_total,
            distancia_total_cm    = Math.Round(dist_total, 2)
        };
    }

    // ── UTILIDADES ────────────────────────────────────────────
    /// <summary>
    /// Extrae el nombre del programa del token IDENTIFIER
    /// que sigue al keyword PROGRAM.
    /// </summary>
    private static string ExtraerNombre(List<Token> tokens)
    {
        var idx = tokens.FindIndex(t => t.Tipo == TokenType.KEYWORD && t.Lexema == "PROGRAM");
        return (idx >= 0 && idx + 1 < tokens.Count) ? tokens[idx + 1].Lexema : "programa";
    }

    /// <summary>
    /// Mapea el enum TokenType a su representación string
    /// para persistir en la BD.
    /// </summary>
    private static string MapToken(TokenType t) => t switch
    {
        TokenType.KEYWORD     => "KEYWORD",
        TokenType.IDENTIFIER  => "IDENTIFIER",
        TokenType.INTEGER     => "INTEGER",
        TokenType.OPERATOR    => "OPERATOR",
        TokenType.PUNCTUATION => "PUNCTUATION",
        TokenType.PARENTHESIS => "PARENTHESIS",
        TokenType.UNKNOWN     => "UNKNOWN",
        _                     => "UNKNOWN"
    };

    /// <summary>
    /// Elimina saltos de línea de strings antes de loggearlos
    /// para prevenir Log Injection.
    /// </summary>
    private static string SanitizarLog(string input)
        => input.Replace(Environment.NewLine, string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty);
}