using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.Services;
using SignalRChatServer.Infrastructure.Records;

namespace SignalRChatServer.API.Hubs;
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ChatInMemory _chatInMemory;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ChatInMemory chatInMemory, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _chatInMemory = chatInMemory;
        _logger = logger;
    }
    public async Task SendMessage(string message)
    {
        var userName = Context.User?.Identity?.Name;
        await Clients.Caller.SendAsync("ReceiveMessage", $"{userName}: {message}");
    }

    #region ConnectionHandling
    //Hanterar vad som ska ske när en användare ansluter till hubben. 
    public override async Task OnConnectedAsync()
    {
        var name = Context.User?.Identity?.Name;
        _logger.LogInformation(name + " connected");
        if (name == null)
        {
            _logger.LogInformation("User not authorized to access the chat.");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "You are not authorized.");
            return;
        }
        //Lägger till användarens användarnamn i en ConcurrentDictionary för att i frmatida tillfällen knyta användarnamn till connectionid och vice versa. 
        _chatInMemory.AddConnectionId(Context.ConnectionId, name);
        //Lägger till användaren i den grupp som den är registrerad för i _chatInMemory ConcurrentDictionary. 
        await Groups.AddToGroupAsync(Context.ConnectionId, _chatInMemory.GetGroupForConnectionId(Context.ConnectionId));
        //Lägger till användaren i en egen grupp för att enklare kunna skicka meddelanden specifikt till användaren ifråga. 
        await Groups.AddToGroupAsync(Context.ConnectionId, name);
        //Samlar ihop all info och skickar till användaren. 
        await SendInitialPayload(name);
        await base.OnConnectedAsync();
    }
    public async Task SendInitialPayload(string user)
    {

        var messages = await _chatService.GetGroupMessages("Lobby");
        var groupUsers = await _chatService.GetGroupUsers("Lobby");
        var groups = await _chatService.GetUsersGroups(user);
        var privateChats = await _chatService.GetUserPrivateChats(user);
        await Clients.Caller.SendAsync("InitialPayload", new { messages, groupUsers, groups, privateChats });
        await UpdateListOfUsersForGroupClients("Lobby");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var name = Context.User?.Identity?.Name;
        _logger.LogInformation(name + " disconnected");
        //Tar bort användaren från ConcurrentDictionary.
        _chatInMemory.RemoveConnectionId(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    #endregion

    #region PrivateChat

    //Hanterar när en användare till öppna en privat chatt med en annan användare.
    public async Task StartPrivateChat(string target)
    {
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} starts private chat with {target}");
        var result = await _chatService.StartPrivateChat(target, currentUser);
        if (result.Success == false || result.Payload == null || result.ConversationId == null)
        {
            _logger.LogInformation($"SendPrivateMessage failed due to {result.Message}.");
            await Clients.Caller.SendAsync("ReceiveError", "System", $"SendPrivateMessage failed due to {result.Message}.");
            return;

        }
        //Lägg till första användaren till en ny grupp för att underlätta att skicka meddelanden till den privata chatten. 
        string conversationId = result.ConversationId.ToString()!;
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        //Hämta connection id för den andra användaren. 
        var targetConnId = _chatInMemory.GetConnectionIdForUser(target);
        if (targetConnId != null)
        {
            //Lägg till andra användaren till en ny grupp för att underlätta att skicka meddelanden till den privata chatten. 

            await Groups.AddToGroupAsync(targetConnId, conversationId);
        }
        //Skicka objektet med all data kring chatten. 
        await Clients.Caller.SendAsync("OpenPrivateChat", result.Payload);
        await Clients.Group(target).SendAsync("OpenPrivateChat", result.Payload);
        _logger.LogInformation("Private chat successfully established.");
    }
    //Hanterar när en användare vill skicka ett meddelande i en privat chatt
    public async Task SendPrivateMessage(Guid conversationId, string message)
    {
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} sends a message in conversationid {conversationId}.");
        var result = await _chatService.SendPrivateMessage(message, conversationId, currentUser);
        if (result.Success == false || result.ChatMessage == null)
        {
            _logger.LogInformation($"SendPrivateMessage failed due to {result.Message}.");
            await Clients.Caller.SendAsync("ReceiveError", "System", $"SendPrivateMessage failed due to {result.Message}.");
            return;

        }
        //Skickar det nya meddelandet till medlemmarna i den privata chatten. 
        await Clients.Group(conversationId.ToString()).SendAsync("ReceivePrivateMessage", new { Username = result.ChatMessage.User.Username, message = result.PrivateMessage, TimeStamp = result.ChatMessage.Timestamp });
        _logger.LogInformation($"Private message sent successfully in conversation {conversationId}");

    }
    #endregion



    #region GroupHandling

    //Hanterar när en användare vill skapa en ny grupp. 
    public async Task StartGroup(string groupName)
    {

        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} tries to create a new group");
        var result = await _chatService.StartGroup(groupName, currentUser);

        if (result.Success == false)
        {
            _logger.LogInformation($"StartGroup failed due to {result.Message}.");
            await Clients.Caller.SendAsync("ReceiveError", "System", result.Message);
            return;

        }
        _logger.LogInformation($"User {currentUser} has successfully created a new group called {groupName}");
        
    }
    //Hanterar när en användare till lägga till en annan användare i en grupp
    public async Task AddUserToGroup(string groupName, string username)
    {
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} is trying to add {username} to group {groupName}.");
        var result = await _chatService.AddUserToGroup(groupName, username, currentUser);
        if (result.Success == false || result.Group == null)
        {
            _logger.LogInformation($"AddUserToGroup failed due to {result.Message}.");
            await Clients.Caller.SendAsync("ReceiveError", "System", result.Message);
            return;
        }
        await SendGroupMessage($"{currentUser} has added {username} to the room.", groupName);
        await Clients.Group(username).SendAsync("ReceiveGroup", new {name = result.Group.Name, owner = result.Group.Owner });
        await UpdateListOfUsersForGroupClients(groupName);
        _logger.LogInformation($"{currentUser} has successfully added {username} to group {groupName}.");

    }
    //Hanterar när en användare försöker skicka ett meddelande till en grupp. 
    public async Task SendGroupMessage(string message, string groupName)
    {
        var username = Context.User?.Identity?.Name;
        _logger.LogInformation($"{username} tries to send a message to group {groupName}");
        var result = await _chatService.SaveGroupMessage(message, groupName, username);
        if (result.Success == false)
        {
            _logger.LogInformation($"SendGroupMessage failed due to {result.Message}.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Failed to send group message.");
            return;

        }
        //Skickar meddelandet till gruppen ifråga. 
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", result.Dto);
        _logger.LogInformation($"{username} has successfully sent a message to group {groupName}");
    }
    //Hanterar när användaren vill byta från en grupp till en annan. 
    public async Task SwitchGroup(string newGroup)
    {
        //Hämtar den gamla gruppen. 
        var oldGroup = _chatInMemory.GetGroupForConnectionId(Context.ConnectionId);
        _logger.LogInformation($"User is trying to switch from group {oldGroup} to group {newGroup}");
        //Tar bort användaren från den gamla gruppen. 
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
        //Lägger till användaren i den nya gruppen. 
        _chatInMemory.UpdateConnectionId(Context.ConnectionId, newGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, newGroup);
        //Skickar infon om den nya gruppen till användaren. 
        await SendGroupDetails(newGroup);
        //Uppdaterar användarlistan för alla i både den nya och gamla gruppen så att 
        await UpdateListOfUsersForGroupClients(newGroup, oldGroup);
        _logger.LogInformation($"User has successfully switched from group {oldGroup} to group {newGroup}");
    }
    //Hanterar när en användare ska tas bort från en grupp. Oanvänd i dagsläget. 
    public async Task RemoveFromGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
    //Hanterar att skicka info om gruppen till användaren. 
    public async Task SendGroupDetails(string group)
    {
        //Hämtar alla meddelanden gällande gruppen. 
        _logger.LogInformation($"Fetching messages for group {group}.");
        var messages = await _chatService.GetGroupMessages(group);
        //Hämtar status på alla gruppens användare.
        _logger.LogInformation($"Fetching user status for group {group}.");
        var groupUsers = await _chatService.GetGroupUsers(group);
        //Skickar info till användaren. 
        await Clients.Caller.SendAsync("SwitchGroupInfo", new { messages, groupUsers });
        _logger.LogInformation($"Sending group messages and user status for group {group}.");
    }
    //Hanterar att plocka bort en grupp. 
    public async Task DeleteGroup(string groupName)
    {
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"User {currentUser} is trying to delete group {groupName}");
        var result = await _chatService.DeleteGroup(groupName);
        if (result.Success == false || result.Users == null)
        {
            _logger.LogInformation($"Delete group failed due to {result.Message}");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Failed to delete group.");
            return;

        }
        //Meddelar alla användare att gruppen försvunnit och att de ska ta bort gruppen.
        await Clients.Groups(result.Users).SendAsync("GroupGone", groupName);
        _logger.LogInformation($"Successfully removed group {groupName}");
    }
    #endregion

    #region Utility
    //Hämtar alla användare för en viss grupp och uppdaterar alla i gruppen. 
    public async Task UpdateListOfUsersForGroupClients(string groupName, string oldGroup = "")
    {
        //Hämtar användarstatus för gruppen. 
        var groupUsers = await _chatService.GetGroupUsers(groupName);
        
        if (oldGroup != "")
        {
            //If oldGroup is defined, update everyone in the old group. 
            var oldGroupUsers = await _chatService.GetGroupUsers(oldGroup);
            await Clients.Group(oldGroup).SendAsync("UpdateListOfUsers", oldGroupUsers);

        }
        //Skickar användarstatus till gruppen. 
        await Clients.Group(groupName).SendAsync("UpdateListOfUsers", groupUsers);
    }
    #endregion
}
