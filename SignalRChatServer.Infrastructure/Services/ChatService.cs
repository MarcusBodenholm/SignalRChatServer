using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.Models;

namespace SignalRChatServer.Infrastructure.Services;

public class ChatService
{
    private Dictionary<string, string> _currentGroup = new();
    private List<string> _groups = new();
    private readonly ChatContext _chatContext;
    //TODO: Fixa till denna.
    //Tanken bakom denna klass är att hålla koll på vilka grupper som finns och vilken grupp en viss connectionId är del (alltså vilken chatt) som en användare är del av. 
    public ChatService(ChatContext chatContext)
    {
        _chatContext = chatContext;
        InitializeChatService();
    }
    private async Task InitializeChatService()
    {
        var allGroups = await _chatContext.Groups.ToListAsync();
        _groups = allGroups.Select(g => g.Name).ToList();
    }
    public async Task<bool> AddGroup(string groupName)
    {
        if (_groups.Contains(groupName))
        {
            return false;
        }
        var group = new Group() { Name = groupName };
        _chatContext.Groups.Add(group);
        await _chatContext.SaveChangesAsync();
        _groups.Add(groupName);
        return true;
    }
    public async Task<bool> RemoveGroup(string groupName)
    {
        if (_groups.Contains(groupName) == false)
        {
            return false;
        }
        var group = _chatContext.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
        _chatContext.Remove(group);
        await _chatContext.SaveChangesAsync();
        _groups.Remove(groupName);
        return true;
    }
    public void AddConnectionId(string connectionId)
    {
        _currentGroup.Add(connectionId, "Lobby");
    }
    public void UpdateConnectionId(string connectionId, string groupName)
    {
        _currentGroup[connectionId] = groupName;
    }
    public string GetGroupForConnectionId(string connectionId)
    {
        return _currentGroup[connectionId];
    }
}