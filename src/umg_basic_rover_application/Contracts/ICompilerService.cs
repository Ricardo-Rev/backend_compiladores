using umg_basic_rover_application.DTOs;

namespace umg_basic_rover_application.Contracts;

public interface ICompilerService
{
    Task<CompileResponse> CompileAsync(CompileRequest request, int usuario_id, int sesion_id);
}
