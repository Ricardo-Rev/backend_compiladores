namespace umg_basic_rover_domain.entities;

public class coreografia_ejecutada_entity
{
    public int      id              { get; set; }
    public int      usuario_id      { get; set; }
    public int      coreografia_id  { get; set; }
    public int?     compilacion_id  { get; set; }
    public bool     modificada      { get; set; } = false;
    public DateTime fecha_ejecucion { get; set; } = DateTime.Now;

    public user_entity         usuario     { get; set; } = null!;
    public coreografia_entity  coreografia { get; set; } = null!;
    public compilacion_entity? compilacion { get; set; }
}
