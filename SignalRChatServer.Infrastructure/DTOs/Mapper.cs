using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Utils;

namespace SignalRChatServer.Infrastructure.DTOs;

public static class Mapper
{
    public static string DecryptMessage(string input)
    {
        if (EncryptionHelper.IsBase64String(input))
        {
            return EncryptionHelper.Decrypt(input);
        }
        return input;

    }
    public static List<GroupMessageDto> MapToGroupMessageDto(List<ChatMessage> messages)
    {
        var result = messages.Select(m =>
        {
            var message = new GroupMessageDto { Message = DecryptMessage(m.Message), Room = m.Group.Name, Username = m.User.Username, TimeStamp = m.Timestamp };
            return message;
        }).ToList();
        return result;
    }
    public static List<PrivateMessageDto> MapToPrivateMessageDto(List<ChatMessage> messages)
    {
        var result = messages.Select(m =>
        {
            var message = new PrivateMessageDto { Message = DecryptMessage(m.Message), TimeStamp = m.Timestamp, Username = m.User.Username };
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