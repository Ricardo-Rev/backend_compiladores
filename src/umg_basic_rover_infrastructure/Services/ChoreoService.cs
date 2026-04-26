using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  ChoreoService
//  Gestiona las 3 coreografías pregrabadas en UMG++.
//  Las siembra automáticamente si no existen en BD (SeedAsync).
// ============================================================

public class ChoreoService : IChoreoService
{
    private readonly rover_db_context      _db;
    private readonly ICompilerService      _compiler;
    private readonly ILogger<ChoreoService> _logger;

    public ChoreoService(rover_db_context db, ICompilerService compiler, ILogger<ChoreoService> logger)
    {
        _db       = db;
        _compiler = compiler;
        _logger   = logger;
    }

    public async Task<List<ChoreoListResponse>> ListarAsync()
    {
        await SeedCoreografiasAsync();

        return await _db.coreografias
            .AsNoTracking()
            .Where(c => c.activa)
            .OrderBy(c => c.id)
            .Select(c => new ChoreoListResponse
            {
                id               = c.id,
                nombre           = c.nombre,
                descripcion      = c.descripcion,
                cancion_nombre   = c.cancion_nombre,
                duracion_min_seg = c.duracion_min_seg
            })
            .ToListAsync();
    }

    public async Task<ChoreoResponse> ObtenerAsync(int coreografia_id)
    {
        var coreo = await _db.coreografias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id == coreografia_id && c.activa)
            ?? throw new KeyNotFoundException($"Coreografía {coreografia_id} no encontrada.");

        return MapToResponse(coreo);
    }

    public async Task<ChoreoResponse> EjecutarAsync(ChoreoExecuteRequest request, int usuario_id, int sesion_id)
    {
        var coreo = await _db.coreografias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id == request.coreografia_id && c.activa)
            ?? throw new KeyNotFoundException($"Coreografía {request.coreografia_id} no encontrada.");

        // Si viene código modificado usar ese, si no usar el original
        var codigo_final = request.modificada && !string.IsNullOrWhiteSpace(request.codigo_modificado)
            ? request.codigo_modificado
            : coreo.codigo_fuente;

        // Compilar SIEMPRE en modo arduino para generar comandos seriales al rover
        int? compilacion_id  = null;
        string? comandos_arduino = null;

        var compile_req = new CompileRequest
        {
            codigo_fuente    = codigo_final,
            modo             = "compilar_ejecutar",
            lenguaje_destino = "arduino"
        };

        var result = await _compiler.CompileAsync(compile_req, usuario_id, sesion_id);
        if (result.exitoso)
        {
            compilacion_id   = result.compilacion_id;
            comandos_arduino = result.codigo_transpilado;
            _logger.LogInformation("[CHOREO] ✅ Comandos Arduino generados para '{n}'", coreo.nombre);
        }
        else
        {
            _logger.LogWarning("[CHOREO] ⚠️ Error al compilar coreografía '{n}'", coreo.nombre);
        }

        // Registrar ejecución en BD
        _db.coreografias_ejecutadas.Add(new coreografia_ejecutada_entity
        {
            usuario_id      = usuario_id,
            coreografia_id  = request.coreografia_id,
            compilacion_id  = compilacion_id,
            modificada      = request.modificada,
            fecha_ejecucion = DateTime.Now
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CHOREO] Coreografía '{n}' ejecutada por usuario {u}", coreo.nombre, usuario_id);

        return new ChoreoResponse
        {
            id               = coreo.id,
            nombre           = coreo.nombre,
            descripcion      = coreo.descripcion,
            codigo_fuente    = codigo_final,
            cancion_url      = coreo.cancion_url,
            cancion_nombre   = coreo.cancion_nombre,
            duracion_min_seg = coreo.duracion_min_seg,
            comandos_arduino = comandos_arduino
        };
    }

    // ── SEED — 3 Coreografías Pregrabadas ───────────────────

    private async Task SeedCoreografiasAsync()
    {
        var count = await _db.coreografias.CountAsync(c => c.activa);
        if (count >= 3) return;

        _logger.LogInformation("[CHOREO] Sembrando coreografías iniciales...");

        var coreografias = new List<coreografia_entity>
        {
            // ── COREOGRAFÍA 1: Thriller (> 3 minutos) ────────
            new()
            {
                nombre           = "Thriller Rover",
                descripcion      = "Coreografía estilo Thriller de Michael Jackson. El rover simula los movimientos icónicos del baile con giros, caminatas y moonwalk.",
                cancion_nombre   = "Thriller - Michael Jackson",
                cancion_url      = "https://www.youtube.com/watch?v=sOnqjkJTMaA",
                duracion_min_seg = 210,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    = @"PROGRAM thriller_rover
BEGIN
    caminar(4);
    girar(1) + avanzar_ctms(30);
    rotar(1);
    moonwalk(3);
    girar(-1) + avanzar_ctms(30);
    rotar(-1);
    caminar(4);
    girar(1) + avanzar_vlts(2);
    circulo(40);
    rotar(2);
    moonwalk(4);
    avanzar_mts(1);
    caminar(6);
    rotar(-2);
    girar(-1) + avanzar_ctms(50);
    moonwalk(2);
    girar(1) + avanzar_ctms(50);
    caminar(3);
    rotar(1);
    circulo(30);
    avanzar_mts(1);
    moonwalk(5);
    rotar(-1);
    caminar(4);
    girar(1) + avanzar_vlts(3);
    rotar(2);
    moonwalk(3);
    girar(-1) + avanzar_mts(1);
    caminar(5);
    rotar(-1);
    circulo(50);
    moonwalk(4);
    caminar(6);
    rotar(1);
    avanzar_ctms(80);
    rotar(-2);
    moonwalk(2);
    caminar(3);
    rotar(1);
END."
            },

            // ── COREOGRAFÍA 2: Cuadros y Círculos (> 3 min) ─
            new()
            {
                nombre           = "Geometría en Movimiento",
                descripcion      = "Secuencia geométrica donde el rover traza cuadrados y círculos de diferentes tamaños combinados con giros y avances.",
                cancion_nombre   = "Around The World - Daft Punk",
                cancion_url      = "https://www.youtube.com/watch?v=K0HSD_i2DvA",
                duracion_min_seg = 195,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    = @"PROGRAM geometria_rover
BEGIN
    cuadrado(50);
    girar(1) + avanzar_ctms(20);
    circulo(25);
    girar(-1) + avanzar_ctms(20);
    cuadrado(80);
    rotar(2);
    circulo(50);
    avanzar_mts(1);
    cuadrado(30);
    girar(1) + avanzar_vlts(2);
    circulo(35);
    rotar(-1);
    cuadrado(60);
    girar(-1) + avanzar_ctms(40);
    circulo(20);
    girar(1) + avanzar_ctms(40);
    cuadrado(100);
    rotar(1);
    circulo(60);
    avanzar_mts(1);
    cuadrado(40);
    rotar(-2);
    circulo(45);
    girar(1) + avanzar_vlts(1);
    cuadrado(70);
    girar(-1) + avanzar_mts(1);
    circulo(55);
    rotar(1);
    cuadrado(90);
    avanzar_ctms(60);
    circulo(30);
    rotar(-1);
    cuadrado(50);
    circulo(40);
    girar(1) + avanzar_mts(1);
    rotar(2);
    cuadrado(60);
END."
            },

            // ── COREOGRAFÍA 3: Moonwalk Festival (> 3 min) ──
            new()
            {
                nombre           = "Moonwalk Festival",
                descripcion      = "Festival de pasos de baile. Moonwalk, caminatas y rotaciones al estilo de Michael Jackson combinados con maniobras del rover.",
                cancion_nombre   = "Billie Jean - Michael Jackson",
                cancion_url      = "https://www.youtube.com/watch?v=Zi_XLOBDo_Y",
                duracion_min_seg = 200,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    = @"PROGRAM moonwalk_festival
BEGIN
    moonwalk(5);
    rotar(1);
    caminar(3);
    girar(1) + avanzar_ctms(25);
    moonwalk(4);
    girar(-1) + avanzar_ctms(25);
    rotar(-1);
    caminar(5);
    moonwalk(6);
    avanzar_mts(1);
    rotar(2);
    caminar(4);
    moonwalk(3);
    girar(1) + avanzar_vlts(2);
    rotar(-1);
    caminar(6);
    moonwalk(5);
    girar(-1) + avanzar_mts(1);
    circulo(35);
    rotar(1);
    moonwalk(4);
    caminar(3);
    avanzar_ctms(50);
    moonwalk(6);
    rotar(-2);
    caminar(5);
    girar(1) + avanzar_ctms(30);
    moonwalk(3);
    girar(-1) + avanzar_ctms(30);
    circulo(25);
    rotar(1);
    caminar(4);
    moonwalk(5);
    avanzar_mts(1);
    rotar(-1);
    caminar(3);
    moonwalk(4);
    rotar(2);
    caminar(5);
    moonwalk(3);
END."
            }
        };

        // Solo agregar las que no existen
        foreach (var coreo in coreografias)
        {
            var existe = await _db.coreografias.AnyAsync(c => c.nombre == coreo.nombre);
            if (!existe) _db.coreografias.Add(coreo);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[CHOREO] ✅ Coreografías sembradas.");
    }

    private static ChoreoResponse MapToResponse(coreografia_entity c) => new()
    {
        id               = c.id,
        nombre           = c.nombre,
        descripcion      = c.descripcion,
        codigo_fuente    = c.codigo_fuente,
        cancion_url      = c.cancion_url,
        cancion_nombre   = c.cancion_nombre,
        duracion_min_seg = c.duracion_min_seg
    };
}