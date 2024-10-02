namespace SignalRChatServer.Infrastructure.Models;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Participant1 { get; set; }
    public required string Participant2 { get; set; }
    public List<ChatMessage> ChatMessages { get; set; } = new();
}