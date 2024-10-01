using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.Models;

namespace SignalRChatServer.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    //TODO: Fixa till hela denna sektionen. 
    private readonly ChatContext _context;

    public ChatHub(ChatContext context)
    {
        _context = context;
    }

    public async void Test(string group)
    {
        _context.Messages.Include(m => m.Group)
            .Where(m => m.Group.Name == group)
            .OrderBy(m => m.Timestamp)
            .Take(50)
            .ToList();
        foreach (var message in _context.Messages)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", message.User.Username, message.Message);
        }
    }
    public async void Post(string message, string groupName, string userName)
    {
        var user = _context.Users.SingleOrDefault(u => u.Username == userName);
        var group = _context.Groups.SingleOrDefault(g => g.Name == groupName);
        ChatMessage chatMessage = new ChatMessage {Message = message, Group = group, User = user};
        _context.Messages.Add(chatMessage);

        await Clients.Group(groupName).SendAsync("Send");
    }
    public async Task AddToGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
    public async Task RemoveFromGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
