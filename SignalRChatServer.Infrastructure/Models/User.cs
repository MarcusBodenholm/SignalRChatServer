namespace SignalRChatServer.Infrastructure.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
}
public class Group
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public virtual List<User> Users { get; set; }
}