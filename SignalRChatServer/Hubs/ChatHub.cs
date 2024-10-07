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
    //TODO: Fixa till hela denna sektionen. 
    private readonly ChatContext _context;
    private readonly ChatService _chatService;
    private readonly ChatInMemory _chatInMemory;

    public ChatHub(ChatContext context, ChatService chatService, ChatInMemory chatInMemory)
    {
        _context = context;
        _chatService = chatService;
        _chatInMemory = chatInMemory;
    }

    public override async Task OnConnectedAsync()
    {
        var name = Context.User?.Identity?.Name;
        if (name == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "You are not authorized.");
            return;
        }
        _chatInMemory.AddConnectionId(Context.ConnectionId, name);
        await Groups.AddToGroupAsync(Context.ConnectionId, _chatInMemory.GetGroupForConnectionId(Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, name);
        await SendInitialPayload(name);
        await base.OnConnectedAsync();
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _chatInMemory.RemoveConnectionId(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    public async Task Post(string message, string groupName, string userName)
    {
        var user = _context.Users.SingleOrDefault(u => u.Username == userName);
        var group = _context.Groups.SingleOrDefault(g => g.Name == groupName);
        string encryptedMessage = EncryptionHelper.Encrypt(message);
        ChatMessage chatMessage = new ChatMessage {Message = encryptedMessage, Group = group, User = user};
        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();
        await Clients.Group(groupName).SendAsync("Send");
    }
    public async Task StartPrivateChat(string target)
    {
        var currentUser = Context.User?.Identity?.Name;
        var targetUser = await _context.Users.SingleOrDefaultAsync(u => u.Username == target);
        if (targetUser == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist0");
            return;
        }
        var conversation = _context.Conversations.SingleOrDefault(c => (c.Participant1 == currentUser && c.Participant2 == target) || (c.Participant1 == target && c.Participant2 == currentUser));
        if (conversation == null)
        {

            conversation = new Conversation { Participant1 = currentUser, Participant2 = target };
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        }
        var rawMessages = await _context.Messages.Include(m => m.User).Include(m => m.Conversation)
            .Where(m => m.Conversation != null && m.Conversation.Id == conversation.Id)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        await Groups.AddToGroupAsync(Context.ConnectionId, conversation.Id.ToString());
        var messages = Mapper.MapToPrivateMessageDto(rawMessages);
        var payload = new {messages, id = conversation.Id, participant1 = conversation.Participant1, participant2 = conversation.Participant2};
        await Groups.AddToGroupAsync(Context.ConnectionId, conversation.Id.ToString());
        var targetConnId = _chatInMemory.GetConnectionIdForUser(target);
        if (targetConnId != null)
        {
            await Groups.AddToGroupAsync(targetConnId, conversation.Id.ToString());
        }
        await Clients.Caller.SendAsync("OpenPrivateChat", payload);
        await Clients.Group(target).SendAsync("OpenPrivateChat", payload);
    }
    public async Task SendPrivateMessage(Guid conversationId, string message)
    {
        var currentUser = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);

        var users = _context.Users.ToList();
        var conversation = await _context.Conversations
            .Include(c => c.ChatMessages)
            .Where(c => c.Id == conversationId)
            .SingleOrDefaultAsync();
        if (conversation == null || user == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist");
            return;

        }
        var chatMessage = new ChatMessage()
        {
            User = user,
            Conversation = conversation,
            Message = EncryptionHelper.Encrypt(message),
            Timestamp = DateTime.UtcNow
        };
        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();

        await Clients.Group(conversationId.ToString()).SendAsync("ReceivePrivateMessage", new { Username = chatMessage.User.Username, message = message, TimeStamp = chatMessage.Timestamp });

    }
    public async Task StartGroup(string groupName)
    {
        var groupExists = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
        var currentUser = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);
        if (groupExists != null || currentUser == null || user == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Group with that name already exists.");
            return;
        }
        var group = new Group { Name = groupName, Owner = currentUser };
        group.Users.Add(user);
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        
    }
    public async Task AddUserToGroup(string groupName, string username)
    {
        var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
        var currentUser = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        if (group == null || user == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Group with that name already exists.");
            return;
        }
        if (group.Owner != currentUser)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Only the owner can add new users.");
            return;
        }
        group.Users.Add(user);
        await _context.SaveChangesAsync();
        await SendGroupMessage($"{currentUser} has added {username} to the room.", groupName);
        await Clients.Group(username).SendAsync("ReceiveGroup", new {name = group.Name, owner = group.Owner });
        await UpdateListOfUsersForGroupClients(groupName);

    }
    public async Task SendGroupMessage(string message, string groupName)
    {
        var username = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
        if (user == null || group == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist");
            return;
        }
        var chatMessage = new ChatMessage 
        { 
            Message = EncryptionHelper.Encrypt(message),
            User = user,
            Group = group,
        };
        _context.Messages.Add(chatMessage);
        var chatMessageDto = new GroupMessageDto { Message = message, Room = chatMessage.Group.Name, Username = chatMessage.User.Username, TimeStamp = chatMessage.Timestamp };
        await _context.SaveChangesAsync();
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", chatMessageDto);

    }

    public async Task SwitchGroup(string newGroup)
    {
        var oldGroup = _chatInMemory.GetGroupForConnectionId(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
        _chatInMemory.UpdateConnectionId(Context.ConnectionId, newGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, newGroup);
        await SendGroupDetails(newGroup);
        await UpdateListOfUsersForGroupClients(newGroup, oldGroup);
    }
    public async Task RemoveFromGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
    public async Task SendGroupDetails(string group)
    {
        var messages = await _chatService.GetGroupMessages(group);
        var groupUsers = await _chatService.GetGroupUsers(group);
        await Clients.Caller.SendAsync("SwitchGroupInfo", new { messages, groupUsers });
    }
    public async Task DeleteGroup(string groupName)
    {
        var currentUser = Context.User?.Identity?.Name;
        var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
        if (currentUser == null || group == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "Group does not exist or...you don't.");
            return;
        }
        var messages = await _context.Messages.Include(m => m.Group).Where(m => m.Group == group).ToListAsync();
        foreach (var message in messages)
        {
            message.Group = null;
        }
        var users = group.Users.Select(u => u.Username).ToList();
        group.Users.Clear();
        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
        foreach (var user in users)
        {
            await Clients.Group(user).SendAsync("GroupGone", groupName);
        }
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
    public async Task UpdateListOfUsersForGroupClients(string groupName, string oldGroup = "")
    {
        var groupUsers = await _chatService.GetGroupUsers(groupName);
        
        if (oldGroup != "")
        {
            //If oldGroup is defined, update everyone in the old group. 
            var oldGroupUsers = await _chatService.GetGroupUsers(oldGroup);
            await Clients.Group(oldGroup).SendAsync("UpdateListOfUsers", oldGroupUsers);

        }
        await Clients.Group(groupName).SendAsync("UpdateListOfUsers", groupUsers);
    }

}
