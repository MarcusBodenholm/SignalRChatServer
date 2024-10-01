using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;

namespace SignalRChatServer.API.Controllers;
public class ChatController : Controller
{
    private readonly ChatContext _context;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatContext context, ILogger<ChatController> logger)
    {
        _context = context;
        _logger = logger;
    }
    [HttpGet("chatrooms/{username}")]
    public async Task<IActionResult> GetUsersRooms(string username)
    {
        _logger.LogInformation("Attempting to find users chatrooms");
        try
        {
            var user = await _context.Users.Include(u => u.Groups)
                .Where(u => u.Username == username).SingleOrDefaultAsync();
            var result = user.Groups.Select(g => g.Name).ToList();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("addusertoroom")]
    public async Task<IActionResult> AddUserToRoom(string grouproom, string username)
    {
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Name == grouproom);
        if (group == null)
        {
            group = new Group { Name = grouproom };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
        }
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return BadRequest("User could not be found");
        }
        group.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok();

    }
    [HttpGet("chatroommessages/{chatroomname}")]
    public async Task<IActionResult> GetChatRoomMessages(string chatroomname)
    {
        var messages = await _context.Messages.Include(m => m.Group).Include(m => m.User).Where(m => m.Group.Name == chatroomname).ToListAsync();
        var result = Mapper.MapToChatMessageDto(messages);
        return Ok(result);
    }
    [HttpPost("addchatmessage")]
    public async Task<IActionResult> AddMessage([FromBody] ChatMessageDto chatMessageDto)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == chatMessageDto.Username);
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Name == chatMessageDto.Room);
        var message = new ChatMessage { Group = group, Message = chatMessageDto.Message, User = user };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return Ok();
    }
}
