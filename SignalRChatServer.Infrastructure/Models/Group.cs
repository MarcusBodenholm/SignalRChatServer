namespace SignalRChatServer.Infrastructure.Models;

public class Group
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public virtual List<User> Users { get; set; }
}