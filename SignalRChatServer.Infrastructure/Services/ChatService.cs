using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using System.ComponentModel;

namespace SignalRChatServer.Infrastructure.Services;

public class ChatService
{
    private readonly ChatContext _context;
    private readonly ILogger<ChatService> _logger;
    private readonly ChatInMemory _chatInMemory;

    //Tanken bakom denna klass är att hålla koll på vilka grupper som finns och vilken grupp en viss connectionId är del (alltså vilken chatt) som en användare är del av. 
    public ChatService(ChatContext context, ILogger<ChatService> logger, ChatInMemory chatInMemory)
    {
        _context = context;
        _logger = logger;
        _chatInMemory = chatInMemory;
    }
    public string GetConnectionIdForUser(string user)
    {
        var kvp = _chatInMemory.currentUsers.SingleOrDefault(kvp => kvp.Value == user);
        return kvp.Key;
    }
    private List<string> GetOnlineGroupUsers(string groupName)
    {
        List<string> connIds = _chatInMemory.currentGroup.Where(kvp => kvp.Value == groupName).Select(kvp => kvp.Key).ToList();
        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();

        return users;
    }
    private List<string> GetAllOnlineUsers()
    {
        List<string> connIds = _chatInMemory.currentGroup.Select(kvp => kvp.Key).ToList();

        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();
        return users;
    }
    public async Task<List<GroupUserDto>> GetGroupUsers(string groupName)
    {
        List<string> allUsers = await _context.Users
            .Include(u => u.Groups)
            .Where(u => u.Groups.Any(g => g.Name == groupName))
            .Select(u => u.Username)
            .ToListAsync();
        List<string> allOnlineUsers = GetAllOnlineUsers();
        List<string> onlineUsers = GetOnlineGroupUsers(groupName);
        List<GroupUserDto> output = allUsers.Select(username =>
        {
            bool isOnline = allOnlineUsers.Contains(username);
            bool isPresent = onlineUsers.Contains(username);
            GroupUserDto groupUserDto = new GroupUserDto { Online = isOnline, Username = username, Present = isPresent };
            return groupUserDto;
        }).ToList();
        return output;
    }
    public async Task<List<GroupDto>> GetUsersGroups(string username)
    {
        var user = await _context.Users.Include(u => u.Groups)
            .Where(u => u.Username == username).SingleOrDefaultAsync();
        var result = user.Groups.Select(g => new GroupDto { Name = g.Name, Owner = g.Owner}).ToList();
        return result;
    }
    public async Task<List<PrivateChatDto>> GetUserPrivateChats(string username)
    {
        var privateChats = await _context.Conversations
            .Where(c => c.Participant1 == username || c.Participant2 == username)
            .ToListAsync();
        var result = Mapper.MapToPrivateChatDto(privateChats);
        return result;
    }
    public async Task<List<GroupMessageDto>> GetGroupMessages(string groupName)
    {
        var rawMessages = await _context.Messages
            .Include(m => m.Group)
            .Include(m => m.User)
            .Where(m => m.Group.Name == groupName)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        var messages = Mapper.MapToGroupMessageDto(rawMessages);
        return messages;
    }
}
