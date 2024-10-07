namespace SignalRChatServer.Infrastructure.DTOs;

public class GroupMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
    public required DateTime TimeStamp { get; set; }


}
