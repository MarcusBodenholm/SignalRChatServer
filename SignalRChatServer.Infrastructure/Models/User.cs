namespace SignalRChatServer.Infrastructure.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public virtual List<Group> Groups { get; set; } = new List<Group>();
}
