using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Records;

namespace SignalRChatServer.Infrastructure.Services;
public interface IChatService
{
    Task<AddUserToGroupResult> AddUserToGroup(string groupName, string username, string currentUser);
    Task<DeleteGroupResult> DeleteGroup(string groupName);
    Task<List<GroupMessageDto>> GetGroupMessages(string groupName);
    Task<List<GroupUserDto>> GetGroupUsers(string groupName);
    Task<List<PrivateChatDto>> GetUserPrivateChats(string username);
    Task<List<GroupDto>> GetUsersGroups(string username);
    Task<SaveGroupMessageResult> SaveGroupMessage(string message, string groupName, string username);
    Task<SendPrivateMessageResult> SendPrivateMessage(string message, Guid conversationId, string currentUser);
    Task<StartGroupResult> StartGroup(string groupName, string currentUser);
    Task<StartPrivateChatResult> StartPrivateChat(string target, string currentUser);
}