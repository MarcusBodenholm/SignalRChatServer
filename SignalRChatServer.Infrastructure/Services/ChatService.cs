using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.Models;
using System.Collections.Concurrent;

namespace SignalRChatServer.Infrastructure.Services;

public class ChatService
{
    private ConcurrentDictionary<string, string> _currentGroup = new();
    private ConcurrentDictionary<string, string> _currentUsers = new();
    //TODO: Fixa till denna.
    //Tanken bakom denna klass är att hålla koll på vilka grupper som finns och vilken grupp en viss connectionId är del (alltså vilken chatt) som en användare är del av. 
    public ChatService()
    {
    }
    //public async Task<bool> AddGroup(string groupName)
    //{
    //    if (_groups.Contains(groupName))
    //    {
    //        return false;
    //    }
    //    var group = new Group() { Name = groupName };
    //    _chatContext.Groups.Add(group);
    //    await _chatContext.SaveChangesAsync();
    //    _groups.Add(groupName);
    //    return true;
    //}
    //public async Task<bool> RemoveGroup(string groupName)
    //{
    //    if (_groups.Contains(groupName) == false)
    //    {
    //        return false;
    //    }
    //    var group = _chatContext.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
    //    _chatContext.Remove(group);
    //    await _chatContext.SaveChangesAsync();
    //    _groups.Remove(groupName);
    //    return true;
    //}
    public void AddConnectionId(string connectionId, string user)
    {
        _currentUsers.TryAdd(connectionId, user);
        _currentGroup.TryAdd(connectionId, "Lobby");
    }
    public void UpdateConnectionId(string connectionId, string groupName)
    {
        _currentGroup[connectionId] = groupName;
    }
    public string GetGroupForConnectionId(string connectionId)
    {
        return _currentGroup[connectionId];
    }
    public string GetUserForConnectionId(string connectionId)
    {
        return _currentUsers[connectionId];
    }
}