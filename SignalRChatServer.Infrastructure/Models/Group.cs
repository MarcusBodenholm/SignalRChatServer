namespace SignalRChatServer.Infrastructure.Models;

public class Group
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Owner { get; set; } = string.Empty;

    public virtual List<User> Users { get; set; } = new List<User>();
}