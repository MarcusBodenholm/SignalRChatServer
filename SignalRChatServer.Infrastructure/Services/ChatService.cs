using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using System.ComponentModel;

namespace SignalRChatServer.Infrastructure.Services;

//denna klass är en hjälpklass till chathubben. 
public class ChatService
{
    private readonly ChatContext _context;
    private readonly ILogger<ChatService> _logger;
    private readonly ChatInMemory _chatInMemory;

    public ChatService(ChatContext context, ILogger<ChatService> logger, ChatInMemory chatInMemory)
    {
        _context = context;
        _logger = logger;
        _chatInMemory = chatInMemory;
    }
    //Denna metod hämtar alla användare för en viss grupp som är online och del av gruppen. 
    private List<string> GetOnlineGroupUsers(string groupName)
    {
        List<string> connIds = _chatInMemory.currentGroup.Where(kvp => kvp.Value == groupName).Select(kvp => kvp.Key).ToList();
        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();

        return users;
    }
    //Denna metod hämtar alla inloggade användare. 
    private List<string> GetAllOnlineUsers()
    {
        List<string> connIds = _chatInMemory.currentGroup.Select(kvp => kvp.Key).ToList();

        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();
        return users;
    }
    //Denna metod hämtar all info om vilka användare som är inloggade och utloggade för en viss grupp. 
    public async Task<List<GroupUserDto>> GetGroupUsers(string groupName)
    {
        //Hämtar alla användare som är del av gruppen. 
        List<string> allUsers = await _context.Users
            .Include(u => u.Groups)
            .Where(u => u.Groups.Any(g => g.Name == groupName))
            .Select(u => u.Username)
            .ToListAsync();
        //Hämtar alla användare som är online. 
        List<string> allOnlineUsers = GetAllOnlineUsers();
        //Hämtar alla användare som är online och aktiv i gruppen just nu. 
        List<string> onlineUsers = GetOnlineGroupUsers(groupName);
        List<GroupUserDto> output = allUsers.Select(username =>
        {
            //Markerar om användaren är online. 
            bool isOnline = allOnlineUsers.Contains(username);
            //Markerar om användaren är online och aktiv i gruppen just nu. 
            bool isPresent = onlineUsers.Contains(username);
            GroupUserDto groupUserDto = new GroupUserDto { Online = isOnline, Username = username, Present = isPresent };
            return groupUserDto;
        }).ToList();
        return output;
    }
    //Denna metod hämtar alla användarens grupper. 
    public async Task<List<GroupDto>> GetUsersGroups(string username)
    {
        var user = await _context.Users.Include(u => u.Groups)
            .Where(u => u.Username == username).SingleOrDefaultAsync();
        var result = user.Groups.Select(g => new GroupDto { Name = g.Name, Owner = g.Owner}).ToList();
        return result;
    }
    //Denna metod hämtar alla användarens privata chatter. 
    public async Task<List<PrivateChatDto>> GetUserPrivateChats(string username)
    {
        var privateChats = await _context.Conversations
            .Where(c => c.Participant1 == username || c.Participant2 == username)
            .ToListAsync();
        var result = Mapper.MapToPrivateChatDto(privateChats);
        return result;
    }
    //Denna metod hämtar alla gruppens meddelanden och mappar samt dekrypterar innehållet. 
    public async Task<List<GroupMessageDto>> GetGroupMessages(string groupName)
    {
        var rawMessages = await _context.Messages
            .Include(m => m.Group)
            .Include(m => m.User)
            .Where(m => m.Group != null && m.Group.Name == groupName)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        var messages = Mapper.MapToGroupMessageDto(rawMessages);
        return messages;
    }
}
