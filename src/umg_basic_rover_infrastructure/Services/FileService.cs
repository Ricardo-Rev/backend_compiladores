using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

public class FileService : IFileService
{
    private readonly rover_db_context          _db;
    private readonly ILogger<FileService>      _logger;

    public FileService(rover_db_context db, ILogger<FileService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<FileResponse> CrearAsync(CreateFileRequest request, int usuario_id)
    {
        _logger.LogInformation("[FILE] Creando archivo '{n}' para usuario {u}", request.nombre_archivo, usuario_id);

        // Validar nombre único por usuario
        var existe = await _db.archivos_umgpp
            .AnyAsync(a => a.usuario_id == usuario_id
                        && a.nombre_archivo == request.nombre_archivo
                        && a.activo);
        if (existe)
            throw new InvalidOperationException($"Ya existe un archivo con el nombre '{request.nombre_archivo}'.");

        var archivo = new archivo_umgpp_entity
        {
            usuario_id         = usuario_id,
            nombre_archivo     = request.nombre_archivo.EndsWith(".umgpp")
                                    ? request.nombre_archivo
                                    : $"{request.nombre_archivo}.umgpp",
            contenido          = request.contenido,
            descripcion        = request.descripcion,
            version            = 1,
            es_coreografia     = false,
            activo             = true,
            fecha_creacion     = DateTime.Now,
            fecha_modificacion = DateTime.Now
        };

        _db.archivos_umgpp.Add(archivo);
        await _db.SaveChangesAsync();

        // Guardar versión inicial en historial
        _db.historial_archivos.Add(new historial_archivo_entity
        {
            archivo_id     = archivo.id,
            usuario_id     = usuario_id,
            version        = 1,
            contenido      = request.contenido,
            comentario     = "Versión inicial",
            fecha_guardado = DateTime.Now
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("[FILE] ✅ Archivo creado. ID: {id}", archivo.id);
        return MapToResponse(archivo);
    }

    public async Task<FileResponse> ObtenerAsync(int archivo_id, int usuario_id)
    {
        var archivo = await _db.archivos_umgpp
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.id == archivo_id
                                   && a.usuario_id == usuario_id
                                   && a.activo)
            ?? throw new KeyNotFoundException($"Archivo {archivo_id} no encontrado.");

        return MapToResponse(archivo);
    }

    public async Task<List<FileListResponse>> ListarAsync(int usuario_id)
    {
        return await _db.archivos_umgpp
            .AsNoTracking()
            .Where(a => a.usuario_id == usuario_id && a.activo)
            .OrderByDescending(a => a.fecha_modificacion)
            .Select(a => new FileListResponse
            {
                id                 = a.id,
                nombre_archivo     = a.nombre_archivo,
                version            = a.version,
                descripcion        = a.descripcion,
                es_coreografia     = a.es_coreografia,
                fecha_modificacion = a.fecha_modificacion
            })
            .ToListAsync();
    }

    public async Task<FileResponse> ActualizarAsync(int archivo_id, UpdateFileRequest request, int usuario_id)
    {
        var archivo = await _db.archivos_umgpp
            .FirstOrDefaultAsync(a => a.id == archivo_id
                                && a.usuario_id == usuario_id
                                && a.activo)
            ?? throw new KeyNotFoundException($"Archivo {archivo_id} no encontrado.");

        var version_anterior = archivo.version;

        // Solo insertar en historial si el código compiló exitosamente
        if (request.guardar_historial)
        {
            var contenido_anterior = archivo.contenido;

            _db.historial_archivos.Add(new historial_archivo_entity
            {
                archivo_id     = archivo.id,
                usuario_id     = usuario_id,
                version        = version_anterior,
                contenido      = contenido_anterior,
                comentario     = request.comentario ?? $"Compilación exitosa v{version_anterior}",
                fecha_guardado = DateTime.Now
            });

            await _db.SaveChangesAsync();

            archivo.version = version_anterior + 1;
        }

        // Siempre actualizar el contenido y fecha
        archivo.contenido          = request.contenido;
        archivo.fecha_modificacion = DateTime.Now;

        await _db.SaveChangesAsync();

        _logger.LogInformation("[FILE] ✅ Archivo {id} actualizado. Historial: {h}", 
            archivo.id, request.guardar_historial);
        return MapToResponse(archivo);
    }
    public async Task EliminarAsync(int archivo_id, int usuario_id)
    {
        var archivo = await _db.archivos_umgpp
            .FirstOrDefaultAsync(a => a.id == archivo_id
                                   && a.usuario_id == usuario_id
                                   && a.activo)
            ?? throw new KeyNotFoundException($"Archivo {archivo_id} no encontrado.");

        // Soft delete
        archivo.activo             = false;
        archivo.fecha_modificacion = DateTime.Now;
        await _db.SaveChangesAsync();
        _logger.LogInformation("[FILE] 🗑 Archivo {id} eliminado (soft delete).", archivo_id);
    }

    public async Task<List<FileListResponse>> ObtenerHistorialAsync(int archivo_id, int usuario_id)
    {
        // Verificar que el archivo pertenece al usuario
        var existe = await _db.archivos_umgpp
            .AnyAsync(a => a.id == archivo_id && a.usuario_id == usuario_id);
        if (!existe)
            throw new KeyNotFoundException($"Archivo {archivo_id} no encontrado.");

        return await _db.historial_archivos
            .AsNoTracking()
            .Where(h => h.archivo_id == archivo_id)
            .OrderByDescending(h => h.version)
            .Select(h => new FileListResponse
            {
                id                 = h.id,
                nombre_archivo     = $"v{h.version}",
                version            = h.version,
                descripcion        = h.comentario,
                es_coreografia     = false,
                fecha_modificacion = h.fecha_guardado
            })
            .ToListAsync();
    }

    private static FileResponse MapToResponse(archivo_umgpp_entity a) => new()
    {
        id                 = a.id,
        nombre_archivo     = a.nombre_archivo,
        contenido          = a.contenido,
        version            = a.version,
        descripcion        = a.descripcion,
        es_coreografia     = a.es_coreografia,
        fecha_creacion     = a.fecha_creacion,
        fecha_modificacion = a.fecha_modificacion
    };
}
