using System.Collections.Concurrent;

namespace SignalRChatServer.Infrastructure.Services;

public class ChatInMemory
{
    public ConcurrentDictionary<string, string> currentGroup = new();
    public ConcurrentDictionary<string, string> currentUsers = new();
    public void AddConnectionId(string connectionId, string user)
    {
        var kvp = currentUsers.SingleOrDefault(kvp => kvp.Value == user);
        if (kvp.Key != null)
        {
            currentUsers.Remove(kvp.Key, out _);
            currentGroup.Remove(kvp.Key, out _);
        }
        currentUsers.TryAdd(connectionId, user);
        currentGroup.TryAdd(connectionId, "Lobby");
    }
    public void RemoveConnectionId(string connectionId)
    {
        currentUsers.Remove(connectionId, out _);
        currentGroup.Remove(connectionId, out _);
    }
    public void UpdateConnectionId(string connectionId, string groupName)
    {
        currentGroup[connectionId] = groupName;
    }
    public string GetGroupForConnectionId(string connectionId)
    {
        return currentGroup[connectionId];
    }
    public string GetUserForConnectionId(string connectionId)
    {
        return currentUsers[connectionId];
    }
    public string GetConnectionIdForUser(string user)
    {
        var kvp = currentUsers.SingleOrDefault(kvp => kvp.Value == user);
        return kvp.Key;
    }

}