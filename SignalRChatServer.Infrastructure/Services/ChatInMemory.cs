using System.Collections.Concurrent;

namespace SignalRChatServer.Infrastructure.Services;

//Den här klassen är en singleton som innehåller två concurrentdictionaries. 
//Den ena för att mappa connectionids med användarnamn. 
//Den andra för att mappa connectionids med grupp. 
//Syftet är att underlätta saker som att uppdatera användarstatus till användare. 
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
    //Plockar bort connectionId från CDs.
    public void RemoveConnectionId(string connectionId)
    {
        currentUsers.Remove(connectionId, out _);
        currentGroup.Remove(connectionId, out _);
    }
    //Uppdaterar gruppen som ett connectionid är associerad med.
    public void UpdateConnectionId(string connectionId, string groupName)
    {
        currentGroup[connectionId] = groupName;
    }
    //Hämtar gruppen för ett connectionid. 
    public string GetGroupForConnectionId(string connectionId)
    {
        return currentGroup[connectionId];
    }
    //Hämtar användarnamn för ett connectionid.
    public string GetUserForConnectionId(string connectionId)
    {
        return currentUsers[connectionId];
    }
    //hämtar connectionid för en användare. 
    public string GetConnectionIdForUser(string user)
    {
        var kvp = currentUsers.SingleOrDefault(kvp => kvp.Value == user);
        return kvp.Key;
    }

}