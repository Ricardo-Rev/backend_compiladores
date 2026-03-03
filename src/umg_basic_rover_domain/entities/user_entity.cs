namespace umg_basic_rover_domain.entities;

public class user_entity
{
    public Guid id { get; set; } = Guid.NewGuid();
    public string name { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
}