namespace SignalRChatServer.Infrastructure.DTOs;

public class ChatMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
}
