namespace SignalRChatServer.Infrastructure.DTOs;

public class PrivateMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required DateTime TimeStamp { get; set; }

}
