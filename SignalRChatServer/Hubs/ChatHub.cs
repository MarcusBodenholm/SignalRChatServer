using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Services;
using SignalRChatServer.Infrastructure.Utils;
using System.Text.Json;

namespace SignalRChatServer.API.Hubs;
[Authorize]
public class ChatHub : Hub
{
    private readonly ChatContext _context;
    private readonly ChatService _chatService;
    private readonly ChatInMemory _chatInMemory;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatContext context, ChatService chatService, ChatInMemory chatInMemory, ILogger<ChatHub> logger)
    {
        _context = context;
        _chatService = chatService;
        _chatInMemory = chatInMemory;
        _logger = logger;
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
        var targetUser = await _context.Users.SingleOrDefaultAsync(u => u.Username == target);
        if (targetUser == null)
        {
            _logger.LogInformation($"{target} could not be found.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist0");
            return;
        }
        var conversation = _context.Conversations.SingleOrDefault(c => (c.Participant1 == currentUser && c.Participant2 == target) || (c.Participant1 == target && c.Participant2 == currentUser));
        if (conversation == null)
        {
            //Om det inte redan finns en chat mellan användarna, skapa en och spara den. 
            conversation = new Conversation { Participant1 = currentUser, Participant2 = target };
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        }
        //Hämta alla meddelanden i rå form. 
        var rawMessages = await _context.Messages.Include(m => m.User).Include(m => m.Conversation)
            .Where(m => m.Conversation != null && m.Conversation.Id == conversation.Id)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        //Mappa meddelanden till dto. Där sker även dekrypteringen.
        var messages = Mapper.MapToPrivateMessageDto(rawMessages);
        //Skapa objektet med all data för båda användarna. 
        var payload = new {messages, id = conversation.Id, participant1 = conversation.Participant1, participant2 = conversation.Participant2};
        //Lägg till första användaren till en ny grupp för att underlätta att skicka meddelanden till den privata chatten. 
        await Groups.AddToGroupAsync(Context.ConnectionId, conversation.Id.ToString());
        //Hämta connection id för den andra användaren. 
        var targetConnId = _chatInMemory.GetConnectionIdForUser(target);
        if (targetConnId != null)
        {
            //Lägg till andra användaren till en ny grupp för att underlätta att skicka meddelanden till den privata chatten. 

            await Groups.AddToGroupAsync(targetConnId, conversation.Id.ToString());
        }
        //Skicka objektet med all data kring chatten. 
        await Clients.Caller.SendAsync("OpenPrivateChat", payload);
        await Clients.Group(target).SendAsync("OpenPrivateChat", payload);
        _logger.LogInformation("Private chat successfully established.");
    }
    //Hanterar när en användare vill skicka ett meddelande i en privat chatt
    public async Task SendPrivateMessage(Guid conversationId, string message)
    {
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} sends a message in conversationid {conversationId}.");
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);
        //Hämtar den existerande privata chatten
        var conversation = await _context.Conversations
            .Include(c => c.ChatMessages)
            .Where(c => c.Id == conversationId)
            .SingleOrDefaultAsync();
        if (conversation == null || user == null)
        {
            _logger.LogInformation($"No conversation wiht conversationid {conversationId}");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist");
            return;

        }
        //Skapar ett nytt chattmeddelande. Meddelandetexten krypteras. 
        var sanitizedMessage = HtmlSanitizer.Sanitize(message);
        var chatMessage = new ChatMessage()
        {
            User = user,
            Conversation = conversation,
            Message = EncryptionHelper.Encrypt(sanitizedMessage),
            Timestamp = DateTime.UtcNow
        };
        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();

        //Skickar det nya meddelandet till medlemmarna i den privata chatten. 
        await Clients.Group(conversationId.ToString()).SendAsync("ReceivePrivateMessage", new { Username = chatMessage.User.Username, message = sanitizedMessage, TimeStamp = chatMessage.Timestamp });
        _logger.LogInformation($"Private message sent successfully in conversation {conversationId}");

    }
    #endregion



    #region GroupHandling

    //Hanterar när en användare vill skapa en ny grupp. 
    public async Task StartGroup(string groupName)
    {

        var groupExists = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
        var currentUser = Context.User?.Identity?.Name;
        _logger.LogInformation($"{currentUser} tries to create a new group");
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);
        if (groupExists != null || currentUser == null || user == null)
        {
            _logger.LogInformation($"Failed creating new group: group by that name already exists.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Group with that name already exists.");
            return;
        }
        var group = new Group { Name = groupName, Owner = currentUser };
        group.Users.Add(user);
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"User {currentUser} has successfully created a new group called {groupName}");
        
    }
    //Hanterar när en användare till lägga till en annan användare i en grupp. 
    public async Task AddUserToGroup(string groupName, string username)
    {
        var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
        var currentUser = Context.User?.Identity?.Name;
        var userToAdd = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        _logger.LogInformation($"{currentUser} is trying to add {username} to group {groupName}.");
        if (group == null || userToAdd == null)
        {
            _logger.LogInformation("Failed to add as either the user or the group does not exist.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Either the group or the user does not exist.");
            return;
        }
        if (group.Owner != currentUser)
        {
            _logger.LogInformation("Failed to add user as only the owner of the group can add new users.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "Only the owner can add new users.");
            return;
        }
        group.Users.Add(userToAdd);
        await _context.SaveChangesAsync();
        await SendGroupMessage($"{currentUser} has added {username} to the room.", groupName);
        await Clients.Group(username).SendAsync("ReceiveGroup", new {name = group.Name, owner = group.Owner });
        await UpdateListOfUsersForGroupClients(groupName);
        _logger.LogInformation($"{currentUser} has successfully added {username} to group {groupName}.");

    }
    //Hanterar när en användare försöker skicka ett meddelande till en grupp. 
    public async Task SendGroupMessage(string message, string groupName)
    {
        var username = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
        _logger.LogInformation($"{username} tries to send a message to group {groupName}");
        if (user == null || group == null)
        {
            _logger.LogInformation($"Failed as the indicated group {groupName} does not exist.");
            await Clients.Caller.SendAsync("ReceiveError", "System", "The indicated group does not exist.");
            return;
        }
        //Saniterar innehållet för att motverka XSS
        var sanitizedMessage = HtmlSanitizer.Sanitize(message);

        var chatMessage = new ChatMessage 
        { 
            Message = EncryptionHelper.Encrypt(sanitizedMessage),
            User = user,
            Group = group,
        };
        _context.Messages.Add(chatMessage);
        var chatMessageDto = new GroupMessageDto { Message = sanitizedMessage, Room = chatMessage.Group.Name, Username = chatMessage.User.Username, TimeStamp = chatMessage.Timestamp };
        await _context.SaveChangesAsync();
        //Skickar meddelandet till gruppen ifråga. 
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", chatMessageDto);
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
        var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
        _logger.LogInformation($"User {currentUser} is trying to delete group {groupName}");
        if (currentUser == null || group == null)
        {
            _logger.LogInformation($"Delete group failed as either the user or the group does not exist.");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "Group does not exist or...you don't.");
            return;
        }
        //Hämtar alla meddelanden. 
        var messages = await _context.Messages.Include(m => m.Group).Where(m => m.Group == group).ToListAsync();
        foreach (var message in messages)
        {
            //Tar bort gruppanknytningen från meddelanden. 
            message.Group = null;
        }
        //Hämtar alla gruppens användare. 
        var users = group.Users.Select(u => u.Username).ToList();
        //Tar bort gruppanknytningen.
        group.Users.Clear();
        _context.Groups.Remove(group);
        //Tar bort gruppen från databasen.
        await _context.SaveChangesAsync();
        foreach (var user in users)
        {
            //Meddelar alla användare att gruppen försvunnit och att de ska ta bort gruppen.
            await Clients.Group(user).SendAsync("GroupGone", groupName);
        }
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
