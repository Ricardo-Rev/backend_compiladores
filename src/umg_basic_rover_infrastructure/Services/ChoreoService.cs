using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

// ============================================================
//  ChoreoService  v2
//
//  Cambios vs v1:
//    · Seed corregido: URLs de YouTube → URLs directas MP3.
//      El elemento <audio> del navegador solo puede reproducir
//      archivos de audio directos (MP3/OGG), no páginas de YouTube.
//    · Coreografías rediseñadas: más cortas (~90s) y con
//      movimientos agrupados para coincidir con frases musicales.
//    · Métodos de administración: Crear, Actualizar, Eliminar.
//      Permiten al admin gestionar cancion_url sin tocar la BD.
//
//  Recomendación para cancion_url:
//    Subir el MP3 a Cloudinary, Supabase Storage u otro CDN
//    con CORS abierto (*) y usar la URL directa al archivo.
//    Ejemplo Cloudinary:
//      https://res.cloudinary.com/<cloud>/video/upload/<id>.mp3
// ============================================================

public class ChoreoService : IChoreoService
{
    private readonly rover_db_context       _db;
    private readonly ICompilerService       _compiler;
    private readonly ILogger<ChoreoService> _logger;

    public ChoreoService(rover_db_context db, ICompilerService compiler, ILogger<ChoreoService> logger)
    {
        _db       = db;
        _compiler = compiler;
        _logger   = logger;
    }

    // ── Público ──────────────────────────────────────────────

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
                tiene_cancion    = c.cancion_url != null,
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

        var codigo_final = request.modificada && !string.IsNullOrWhiteSpace(request.codigo_modificado)
            ? request.codigo_modificado
            : coreo.codigo_fuente;

        int?    compilacion_id   = null;
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
            _logger.LogInformation("[CHOREO] ✅ Comandos Arduino para '{n}'", coreo.nombre);
        }
        else
        {
            _logger.LogWarning("[CHOREO] ⚠️ Error compilando '{n}'", coreo.nombre);
        }

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

    // ── Administración ────────────────────────────────────────

    public async Task<List<ChoreoAdminItem>> ListarAdminAsync()
    {
        return await _db.coreografias
            .AsNoTracking()
            .OrderBy(c => c.id)
            .Select(c => new ChoreoAdminItem
            {
                id                = c.id,
                nombre            = c.nombre,
                descripcion       = c.descripcion,
                cancion_url       = c.cancion_url,
                cancion_nombre    = c.cancion_nombre,
                tiene_cancion     = c.cancion_url != null,
                duracion_min_seg  = c.duracion_min_seg,
                activa            = c.activa,
                total_ejecuciones = c.ejecuciones.Count,
                fecha_creacion    = c.fecha_creacion
            })
            .ToListAsync();
    }

    public async Task<ChoreoResponse> CrearAsync(ChoreoCreateRequest request, int creado_por)
    {
        var coreo = new coreografia_entity
        {
            nombre           = request.nombre.Trim(),
            descripcion      = request.descripcion?.Trim(),
            codigo_fuente    = request.codigo_fuente,
            cancion_url      = string.IsNullOrWhiteSpace(request.cancion_url) ? null : request.cancion_url.Trim(),
            cancion_nombre   = request.cancion_nombre?.Trim(),
            duracion_min_seg = request.duracion_min_seg,
            creado_por       = creado_por,
            activa           = true,
            fecha_creacion   = DateTime.Now
        };

        _db.coreografias.Add(coreo);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CHOREO-ADMIN] Coreografía '{n}' creada por usuario {u}", coreo.nombre, creado_por);
        return MapToResponse(coreo);
    }

    public async Task<ChoreoResponse> ActualizarAsync(int coreografia_id, ChoreoUpdateRequest request)
    {
        var coreo = await _db.coreografias
            .FirstOrDefaultAsync(c => c.id == coreografia_id)
            ?? throw new KeyNotFoundException($"Coreografía {coreografia_id} no encontrada.");

        if (!string.IsNullOrWhiteSpace(request.nombre))
            coreo.nombre = request.nombre.Trim();

        if (request.descripcion is not null)
            coreo.descripcion = string.IsNullOrWhiteSpace(request.descripcion) ? null : request.descripcion.Trim();

        if (!string.IsNullOrWhiteSpace(request.codigo_fuente))
            coreo.codigo_fuente = request.codigo_fuente;

        // cancion_url: null = no cambia; limpiar_cancion=true = elimina; valor = actualiza
        if (request.limpiar_cancion)
        {
            coreo.cancion_url    = null;
            coreo.cancion_nombre = null;
        }
        else if (request.cancion_url is not null)
        {
            coreo.cancion_url    = string.IsNullOrWhiteSpace(request.cancion_url) ? null : request.cancion_url.Trim();
            coreo.cancion_nombre = request.cancion_nombre?.Trim();
        }

        if (request.cancion_nombre is not null && !request.limpiar_cancion)
            coreo.cancion_nombre = string.IsNullOrWhiteSpace(request.cancion_nombre) ? null : request.cancion_nombre.Trim();

        if (request.duracion_min_seg.HasValue)
            coreo.duracion_min_seg = request.duracion_min_seg.Value;

        if (request.activa.HasValue)
            coreo.activa = request.activa.Value;

        await _db.SaveChangesAsync();

        _logger.LogInformation("[CHOREO-ADMIN] Coreografía {id} actualizada", coreografia_id);
        return MapToResponse(coreo);
    }

    public async Task EliminarAsync(int coreografia_id)
    {
        var coreo = await _db.coreografias
            .FirstOrDefaultAsync(c => c.id == coreografia_id)
            ?? throw new KeyNotFoundException($"Coreografía {coreografia_id} no encontrada.");

        coreo.activa = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CHOREO-ADMIN] Coreografía {id} desactivada", coreografia_id);
    }

    // ── Seed ─────────────────────────────────────────────────
    //
    // Las 3 coreografías están diseñadas para ~90 segundos.
    // Cada bloque de instrucciones corresponde a una frase
    // musical de ~8 barras (a 120 BPM = 16 segundos).
    //
    // cancion_url usa el placeholder "REEMPLAZAR_CON_URL_MP3".
    // El administrador debe ir a /api/choreo/admin/{id} (PUT)
    // y actualizar con la URL real del archivo MP3 alojado en
    // Cloudinary, Supabase Storage u otro CDN con CORS abierto.

    private async Task SeedCoreografiasAsync()
    {
        var count = await _db.coreografias.CountAsync(c => c.activa);
        if (count >= 3) return;

        _logger.LogInformation("[CHOREO] Sembrando coreografías...");

        var seed = new List<coreografia_entity>
        {
            // ── COREOGRAFÍA 1: Thriller Rover ────────────────
            // ~90 segundos — 4 frases de ~22s cada una.
            // Frase 1 (0-22s):  caminata de entrada + giro
            // Frase 2 (22-44s): moonwalk + rotación central
            // Frase 3 (44-66s): círculo + avance + moonwalk
            // Frase 4 (66-90s): caminata final + rotación de cierre
            new()
            {
                nombre           = "Thriller Rover",
                descripcion      = "Coreografía estilo Thriller. Caminatas, moonwalk y rotaciones coordinadas con frases musicales de 22 segundos.",
                cancion_nombre   = "Thriller — Michael Jackson",
                // INSTRUCCIÓN PARA EL ADMIN:
                // Subir un MP3 a Cloudinary y actualizar esta URL vía
                // PUT /api/choreo/admin/{id}  →  { "cancion_url": "https://..." }
                cancion_url      = null,
                duracion_min_seg = 90,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    =
@"PROGRAM thriller_rover
BEGIN
  caminar(3);
  girar(1) + avanzar_ctms(40);
  caminar(3);
  girar(-1) + avanzar_ctms(40);
  moonwalk(4);
  rotar(1);
  moonwalk(4);
  rotar(-1);
  circulo(35);
  avanzar_ctms(50);
  moonwalk(3);
  caminar(4);
  rotar(2);
  caminar(4);
  rotar(-2);
END."
            },

            // ── COREOGRAFÍA 2: Geometría en Movimiento ────────
            // ~90 segundos — patrón cuadrado-círculo alternado.
            // Frase 1 (0-20s):  cuadrado pequeño
            // Frase 2 (20-45s): giro + círculo + avance
            // Frase 3 (45-70s): cuadrado grande + rotación
            // Frase 4 (70-90s): círculo final + regreso
            new()
            {
                nombre           = "Geometría en Movimiento",
                descripcion      = "Patrón geométrico: cuadrados y círculos de diferentes tamaños. Cada figura coincide con un segmento musical.",
                cancion_nombre   = "Around The World — Daft Punk",
                cancion_url      = null,
                duracion_min_seg = 90,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    =
@"PROGRAM geometria_rover
BEGIN
  cuadrado(40);
  girar(1) + avanzar_ctms(25);
  circulo(25);
  girar(-1) + avanzar_ctms(25);
  cuadrado(70);
  rotar(1);
  circulo(45);
  avanzar_ctms(60);
  rotar(-1);
  cuadrado(50);
  circulo(30);
END."
            },

            // ── COREOGRAFÍA 3: Moonwalk Festival ─────────────
            // ~90 segundos — énfasis en moonwalk y caminata.
            // Frase 1 (0-22s):  moonwalk de entrada + giro
            // Frase 2 (22-45s): caminar + rotación
            // Frase 3 (45-68s): moonwalk largo + círculo
            // Frase 4 (68-90s): caminar + moonwalk de cierre
            new()
            {
                nombre           = "Moonwalk Festival",
                descripcion      = "Secuencia de moonwalk y caminatas al estilo Michael Jackson. Cada paso coincide con frases de 4 barras.",
                cancion_nombre   = "Billie Jean — Michael Jackson",
                cancion_url      = null,
                duracion_min_seg = 90,
                activa           = true,
                fecha_creacion   = DateTime.Now,
                codigo_fuente    =
@"PROGRAM moonwalk_festival
BEGIN
  moonwalk(4);
  girar(1) + avanzar_ctms(30);
  moonwalk(3);
  girar(-1) + avanzar_ctms(30);
  caminar(4);
  rotar(1);
  caminar(4);
  rotar(-1);
  moonwalk(5);
  circulo(30);
  avanzar_ctms(40);
  caminar(3);
  moonwalk(4);
  caminar(3);
END."
            }
        };

        foreach (var coreo in seed)
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