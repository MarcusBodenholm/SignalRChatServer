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
public class GroupMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
    public required DateTime TimeStamp { get; set; }


}
public class PrivateMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required DateTime TimeStamp { get; set; }

}
public class PrivateChatDto
{
    public string Participant1 { get; set; }
    public string Participant2 { get; set; }
    public Guid Id { get; set; }
}
public class GroupDto
{
    public string Name { get; set; }
    public string Owner {  get; set; }
}

public static class Mapper
{
    public static List<GroupMessageDto> MapToGroupMessageDto(List<ChatMessage> messages)
    {
        var result = messages.Select(m =>
        {
            var message = new GroupMessageDto { Message = m.Message, Room = m.Group.Name, Username = m.User.Username, TimeStamp = m.Timestamp };
            return message;
        }).ToList();
        return result;
    }
    public static List<PrivateMessageDto> MapToPrivateMessageDto(List<ChatMessage> messages)
    {
        var result = messages.Select(m =>
        {
            var message = new PrivateMessageDto { Message = m.Message, TimeStamp = m.Timestamp, Username = m.User.Username };
            return message;
        }).ToList();
        return result;
    }
    public static List<PrivateChatDto> MapToPrivateChatDto(List<Conversation> convos)
    {
        var result = convos.Select(c =>
        {
            var chat = new PrivateChatDto { Id = c.Id, Participant1 = c.Participant1, Participant2 = c.Participant2 };
            return chat;
        }).ToList();
        return result;
    }

}