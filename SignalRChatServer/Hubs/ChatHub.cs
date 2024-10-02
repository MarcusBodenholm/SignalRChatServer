using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Services;

namespace SignalRChatServer.API.Hubs;
[Authorize]
public class ChatHub : Hub
{
    //TODO: Fixa till hela denna sektionen. 
    private readonly ChatContext _context;
    private readonly ChatService _chatService;

    public ChatHub(ChatContext context, ChatService chatService)
    {
        _context = context;
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var name = Context.User?.Identity?.Name;
        if (name == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "You are not authorized.");
            return;
        }
        _chatService.AddConnectionId(Context.ConnectionId, name);
        await Groups.AddToGroupAsync(Context.ConnectionId, _chatService.GetGroupForConnectionId(Context.ConnectionId));
        await SendExistingGroupMessages("Lobby");
        await base.OnConnectedAsync();
    }
    public async Task Post(string message, string groupName, string userName)
    {
        var user = _context.Users.SingleOrDefault(u => u.Username == userName);
        var group = _context.Groups.SingleOrDefault(g => g.Name == groupName);
        ChatMessage chatMessage = new ChatMessage {Message = message, Group = group, User = user};
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
            .Where(m => m.Conversation.Id == conversation.Id)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        var messages = Mapper.MapToPrivateMessageDto(rawMessages);

        await Clients.Caller.SendAsync("OpenPrivateChat", conversation.Id, target, messages);
        await Clients.Caller.SendAsync("OpenPrivateChat", conversation.Id, currentUser, messages);
    }
    public async Task SendPrivateMessage(Guid conversationId, string message)
    {
        var currentUser = Context.User?.Identity?.Name;
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);

        var users = _context.Users.ToList();
        var conversation = await _context.Conversations
            .Include(c => c.ChatMessages)
            .SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conversation == null || user == null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "System", "Target user does not exist0");
            return;

        }
        var chatMessage = new ChatMessage()
        {
            User = user,
            Conversation = conversation,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();

        await Clients.Users(conversation.Participant1).SendAsync("ReceivePrivateMessage", conversation.Id, currentUser, message);
        await Clients.Users(conversation.Participant2).SendAsync("ReceivePrivateMessage", conversation.Id, currentUser, message);

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
            Message = message,
            User = user,
            Group = group,
        };
        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", user.Username, message);

    }

    public async Task SwitchGroup(string newGroup)
    {
        var oldGroup = _chatService.GetGroupForConnectionId(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
        _chatService.UpdateConnectionId(Context.ConnectionId, newGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, newGroup);
        await SendExistingGroupMessages(newGroup);
    }
    public async Task RemoveFromGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
    public async Task SendExistingGroupMessages(string group)
    {
        var rawMessages = _context.Messages.Include(m => m.Group)
            .Include(m => m.User)
            .Where(m => m.Group != null && m.Group.Name == group)
            .OrderBy(m => m.Timestamp)
            .ToList();
        var messages = Mapper.MapToGroupMessageDto(rawMessages);
        foreach (var message in messages)
        {
            await Clients.Caller.SendAsync("ReceiveGroupMessage", message.Username, message.Message);
        }

    }
}
