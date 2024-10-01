using SignalRChatServer.Infrastructure.Models;

namespace SignalRChatServer.Infrastructure.DTOs;

public class UserDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
}
public class ChatMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
}

public static class Mapper
{
    public static List<ChatMessageDto> MapToChatMessageDto(List<ChatMessage> messages)
    {
        var result = messages.Select(m =>
        {
            var message = new ChatMessageDto { Message = m.Message, Room = m.Group.Name, Username = m.User.Username };
            return message;
        }).ToList();
        return result;
    }
}