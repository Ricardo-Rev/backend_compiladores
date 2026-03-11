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

public class CompilerService : ICompilerService
{
    private readonly rover_db_context _db;
    private readonly ILogger<CompilerService> _logger;

    public CompilerService(rover_db_context db, ILogger<CompilerService> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task<CompileResponse> CompileAsync(CompileRequest request, int usuario_id, int sesion_id)
    {
        var safeModo = request.modo?.Replace(Environment.NewLine, string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
_logger.LogInformation("[COMPILER] Iniciando compilación. Usuario: {u} | Modo: {m}", usuario_id, safeModo);
        var sw = Stopwatch.StartNew();
        var response = new CompileResponse();

        // FASE 1: LÉXICO
        var lexer = new Lexer(request.codigo_fuente);
        var (tokens, errores_lex) = lexer.Tokenize();
        response.tokens = tokens.Where(t => t.Tipo != TokenType.EOF)
            .Select(t => new TokenDto { linea = t.Linea, columna = t.Columna, tipo = t.Tipo.ToString(), lexema = t.Lexema, valor = t.Valor }).ToList();

        if (errores_lex.Any())
        {
            sw.Stop();
            response.exitoso = false; response.resultado = "error_lexico"; response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores = errores_lex.Select(e => new ErrorDto { tipo = "lexico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_lexico", tokens, errores_lex, new(), response.tiempo_ms);
            return response;
        }

        // FASE 2: SINTÁCTICO
        var parser = new Parser(tokens);
        var (nodos, errores_sin) = parser.Parse();

        if (errores_sin.Any())
        {
            sw.Stop();
            response.exitoso = false; response.resultado = "error_sintactico"; response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores = errores_sin.Select(e => new ErrorDto { tipo = "sintactico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_sintactico", tokens, errores_sin, new(), response.tiempo_ms);
            return response;
        }

        // FASE 3: SEMÁNTICO
        var semantic = new SemanticAnalyzer(nodos);
        var (instrucciones, errores_sem) = semantic.Analyze();

        if (errores_sem.Any())
        {
            sw.Stop();
            response.exitoso = false; response.resultado = "error_semantico"; response.tiempo_ms = (int)sw.ElapsedMilliseconds;
            response.errores = errores_sem.Select(e => new ErrorDto { tipo = "semantico", mensaje = e }).ToList();
            await Persistir(request, usuario_id, sesion_id, "error_semantico", tokens, errores_sem, new(), response.tiempo_ms);
            return response;
        }

        // FASE 4: TRANSPILACIÓN
        var transpiler = new Transpiler();
        var nombre     = ExtraerNombre(tokens);
        response.codigo_transpilado = request.lenguaje_destino == "csharp"
            ? transpiler.TranspilarACSharp(nombre, instrucciones)
            : transpiler.TranspilarAPython(nombre, instrucciones);

        // FASE 5: SIMULACIÓN
        SimulacionDto? sim = null;
        if (request.modo is "compilar_simular" or "compilar_ejecutar")
            sim = GenerarSimulacion(instrucciones);

        sw.Stop();
        response.tiempo_ms = (int)sw.ElapsedMilliseconds;
        var comp_id = await Persistir(request, usuario_id, sesion_id, "exito", tokens, new(), instrucciones, response.tiempo_ms, sim);

        response.exitoso        = true;
        response.resultado      = "exito";
        response.compilacion_id = comp_id;
        response.simulacion     = sim;
        response.instrucciones  = instrucciones.Select(i => new InstruccionDto
            { orden = i.Orden, nombre = i.Nombre, raw = i.Raw, parametro_n = i.ParametroN, parametro_r = i.ParametroR, parametro_l = i.ParametroL }).ToList();

        _logger.LogInformation("[COMPILER] ✅ Éxito en {ms}ms. ID: {id}", response.tiempo_ms, comp_id);
        return response;
    }

    private async Task<int> Persistir(CompileRequest req, int uid, int sid, string resultado,
        List<Token> tokens, List<string> errores, List<InstruccionValidada> instrucciones,
        int tiempo_ms, SimulacionDto? sim = null)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var comp = new compilacion_entity
            {
                usuario_id = uid, sesion_id = sid, archivo_id = req.archivo_id,
                codigo_fuente = req.codigo_fuente, modo_compilacion = req.modo,
                resultado = resultado, tiempo_compilacion_ms = tiempo_ms, fecha_compilacion = DateTime.Now
            };
            _db.compilaciones.Add(comp);
            await _db.SaveChangesAsync();

            _db.tokens_lexer.AddRange(tokens.Where(t => t.Tipo != TokenType.EOF).Select(t => new token_lexer_entity
            {
                compilacion_id = comp.id, numero_linea = t.Linea, numero_columna = t.Columna,
                tipo_token = MapToken(t.Tipo), lexema = t.Lexema, valor = t.Valor
            }));

            if (errores.Any())
            {
                var tipo = resultado.Replace("error_", "");
                _db.errores_compilacion.AddRange(errores.Select(e => new error_compilacion_entity
                    { compilacion_id = comp.id, tipo_error = tipo, mensaje_error = e }));
            }

            if (instrucciones.Any())
            {
                var nombres  = instrucciones.Where(i => !i.EsCombinada).Select(i => i.Nombre).Distinct().ToList();
                var catalogo = await _db.instrucciones_umgpp.Where(x => nombres.Contains(x.nombre_instruccion))
                    .ToDictionaryAsync(x => x.nombre_instruccion, x => x.id);

                foreach (var inst in instrucciones)
                {
                    var cat_id = inst.EsCombinada ? (catalogo.Values.FirstOrDefault() == 0 ? 1 : catalogo.Values.First())
                                                  : (catalogo.TryGetValue(inst.Nombre, out var cid) ? cid : 1);
                    _db.instrucciones_ejecutadas.Add(new instruccion_ejecutada_entity
                    {
                        compilacion_id = comp.id, numero_orden = inst.Orden, instruccion_id = cat_id,
                        parametro_n = inst.ParametroN, parametro_r = inst.ParametroR, parametro_l = inst.ParametroL,
                        instruccion_raw = inst.Raw
                    });
                }
            }

            if (sim != null)
            {
                var s = new simulacion_entity
                {
                    compilacion_id = comp.id, usuario_id = uid,
                    trayectoria_json = sim.trayectoria_json,
                    duracion_estimada_seg = sim.duracion_estimada_seg,
                    distancia_total_cm = sim.distancia_total_cm, fecha_simulacion = DateTime.Now
                };
                _db.simulaciones.Add(s);
                await _db.SaveChangesAsync();
                sim.simulacion_id = s.id;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return comp.id;
        }
        catch (Exception ex) { await tx.RollbackAsync(); _logger.LogError(ex, "[COMPILER] Error al persistir"); throw; }
    }

    private SimulacionDto GenerarSimulacion(List<InstruccionValidada> instrucciones)
    {
        var puntos = new List<object>(); decimal dist = 0; int dur = 0;
        foreach (var i in instrucciones.OrderBy(x => x.Orden))
        {
            switch (i.Nombre)
            {
                case "avanzar_vlts":  var cv = (i.ParametroN ?? 0) * 20;  dist += Math.Abs(cv);  dur += Math.Abs(i.ParametroN ?? 0) * 2; puntos.Add(new { tipo = "avanzar", valor = i.ParametroN, unidad = "vueltas",      cm = cv }); break;
                case "avanzar_ctms":                                        dist += Math.Abs(i.ParametroN ?? 0); dur += (int)(Math.Abs(i.ParametroN ?? 0) * 0.5); puntos.Add(new { tipo = "avanzar", valor = i.ParametroN, unidad = "centimetros", cm = i.ParametroN }); break;
                case "avanzar_mts":   var cm = (i.ParametroN ?? 0) * 100; dist += Math.Abs(cm);  dur += Math.Abs(i.ParametroN ?? 0) * 5; puntos.Add(new { tipo = "avanzar", valor = i.ParametroN, unidad = "metros",       cm }); break;
                case "girar":         dur += 2; var dir = (i.ParametroN ?? 0) switch { 1 => "derecha", -1 => "izquierda", _ => "recto" }; puntos.Add(new { tipo = "girar", direccion = dir }); break;
                case "circulo":       var circ = (decimal)(2 * Math.PI * (i.ParametroR ?? 0)); dist += circ; dur += (i.ParametroR ?? 0) / 10; puntos.Add(new { tipo = "circulo", radio = i.ParametroR }); break;
                case "cuadrado":      var lado = i.ParametroL ?? 0; dist += lado * 4; dur += lado / 5; puntos.Add(new { tipo = "cuadrado", lado }); break;
                case "rotar":         dur += Math.Abs(i.ParametroN ?? 0) * 3; puntos.Add(new { tipo = "rotar", vueltas = i.ParametroN }); break;
                case "caminar":       dur += Math.Abs(i.ParametroN ?? 0) * 2; puntos.Add(new { tipo = "caminar", pasos = i.ParametroN }); break;
                case "moonwalk":      dur += Math.Abs(i.ParametroN ?? 0) * 3; puntos.Add(new { tipo = "moonwalk", pasos = i.ParametroN }); break;
                case "combinada":     dur += 5; puntos.Add(new { tipo = "combinada", raw = i.Raw }); break;
            }
        }
        return new SimulacionDto { trayectoria_json = JsonSerializer.Serialize(puntos), duracion_estimada_seg = dur, distancia_total_cm = Math.Round(dist, 2) };
    }

    private string ExtraerNombre(List<Token> tokens)
    {
        var idx = tokens.FindIndex(t => t.Tipo == TokenType.KEYWORD && t.Lexema == "PROGRAM");
        return (idx >= 0 && idx + 1 < tokens.Count) ? tokens[idx + 1].Lexema : "programa";
    }

    private string MapToken(TokenType t) => t switch
    {
        TokenType.KEYWORD => "KEYWORD", TokenType.IDENTIFIER => "IDENTIFIER", TokenType.INTEGER => "INTEGER",
        TokenType.OPERATOR => "OPERATOR", TokenType.PUNCTUATION => "PUNCTUATION", TokenType.PARENTHESIS => "PARENTHESIS",
        _ => "UNKNOWN"
    };
}
