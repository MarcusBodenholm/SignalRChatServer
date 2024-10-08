using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Records;
using SignalRChatServer.Infrastructure.Utils;

namespace SignalRChatServer.Infrastructure.Services;

//denna klass är en hjälpklass till chathubben. 
public class ChatService : IChatService
{
    private readonly ChatContext _context;
    private readonly ILogger<ChatService> _logger;
    private readonly ChatInMemory _chatInMemory;

    public ChatService(ChatContext context, ILogger<ChatService> logger, ChatInMemory chatInMemory)
    {
        _context = context;
        _logger = logger;
        _chatInMemory = chatInMemory;
    }
    //Denna metod hämtar alla användare för en viss grupp som är online och del av gruppen. 
    private List<string> GetOnlineGroupUsers(string groupName)
    {
        List<string> connIds = _chatInMemory.currentGroup.Where(kvp => kvp.Value == groupName).Select(kvp => kvp.Key).ToList();
        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();

        return users;
    }
    //Denna metod hämtar alla inloggade användare. 
    private List<string> GetAllOnlineUsers()
    {
        List<string> connIds = _chatInMemory.currentGroup.Select(kvp => kvp.Key).ToList();

        List<string> users = _chatInMemory.currentUsers.Where(kvp => connIds.Contains(kvp.Key)).Select(kvp => kvp.Value).ToList();
        return users;
    }
    //Denna metod hämtar all info om vilka användare som är inloggade och utloggade för en viss grupp. 
    public async Task<List<GroupUserDto>> GetGroupUsers(string groupName)
    {
        //Hämtar alla användare som är del av gruppen. 
        List<string> allUsers = await _context.Users
            .Include(u => u.Groups)
            .Where(u => u.Groups.Any(g => g.Name == groupName))
            .Select(u => u.Username)
            .ToListAsync();
        //Hämtar alla användare som är online. 
        List<string> allOnlineUsers = GetAllOnlineUsers();
        //Hämtar alla användare som är online och aktiv i gruppen just nu. 
        List<string> onlineUsers = GetOnlineGroupUsers(groupName);
        List<GroupUserDto> output = allUsers.Select(username =>
        {
            //Markerar om användaren är online. 
            bool isOnline = allOnlineUsers.Contains(username);
            //Markerar om användaren är online och aktiv i gruppen just nu. 
            bool isPresent = onlineUsers.Contains(username);
            GroupUserDto groupUserDto = new GroupUserDto { Online = isOnline, Username = username, Present = isPresent };
            return groupUserDto;
        }).ToList();
        return output;
    }
    //Denna metod hämtar alla användarens grupper. 
    public async Task<List<GroupDto>> GetUsersGroups(string username)
    {
        var user = await _context.Users.Include(u => u.Groups)
            .Where(u => u.Username == username).SingleOrDefaultAsync();
        var result = user.Groups.Select(g => new GroupDto { Name = g.Name, Owner = g.Owner }).ToList();
        return result;
    }
    //Denna metod hämtar alla användarens privata chatter. 
    public async Task<List<PrivateChatDto>> GetUserPrivateChats(string username)
    {
        var privateChats = await _context.Conversations
            .Where(c => c.Participant1 == username || c.Participant2 == username)
            .ToListAsync();
        var result = Mapper.MapToPrivateChatDto(privateChats);
        return result;
    }
    //Denna metod hämtar alla gruppens meddelanden och mappar samt dekrypterar innehållet. 
    public async Task<List<GroupMessageDto>> GetGroupMessages(string groupName)
    {
        var rawMessages = await _context.Messages
            .Include(m => m.Group)
            .Include(m => m.User)
            .Where(m => m.Group != null && m.Group.Name == groupName)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        var messages = Mapper.MapToGroupMessageDto(rawMessages);
        return messages;
    }
    public async Task<DeleteGroupResult> DeleteGroup(string groupName)
    {
        try
        {
            var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
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
            return new DeleteGroupResult(true, "", users);

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR ChatService > DeleteGroup :: {ex.Message}");
            return new DeleteGroupResult(false, ex.Message, null);
        }
    }
    public async Task<SaveGroupMessageResult> SaveGroupMessage(string message, string groupName, string username)
    {
        try
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            var group = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
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
            return new SaveGroupMessageResult(true, "", chatMessageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR ChatService > SaveGroupMessage :: {ex.Message}");
            return new SaveGroupMessageResult(false, ex.Message, null);
        }
    }
    public async Task<SendPrivateMessageResult> SendPrivateMessage(string message, Guid conversationId, string currentUser)
    {
        try
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);
            //Hämtar den existerande privata chatten
            var conversation = await _context.Conversations
                .Include(c => c.ChatMessages)
                .Where(c => c.Id == conversationId)
                .SingleOrDefaultAsync();
            if (conversation == null || user == null)
            {
                _logger.LogInformation($"No conversation with conversationid {conversationId} found.");
                return new SendPrivateMessageResult(false, $"No conversation with conversationid {conversationId} found.", "", null);

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
            return new SendPrivateMessageResult(true, "", sanitizedMessage, chatMessage);

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR ChatService > SendPrivateMessage :: {ex.Message}");
            return new SendPrivateMessageResult(false, ex.Message, "", null);
        }

    }
    public async Task<AddUserToGroupResult> AddUserToGroup(string groupName, string username, string currentUser)
    {
        try
        {
            var group = await _context.Groups.Include(g => g.Users).SingleOrDefaultAsync(g => g.Name == groupName);
            var userToAdd = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (group == null || userToAdd == null)
            {
                //Om varken grupp eller användare kan hittas i DB så svarar metoden med ett misslyckande.
                _logger.LogInformation("Failed to add as either the user or the group does not exist.");
                return new AddUserToGroupResult(false, "The specified group or user could not be found", null);
            }
            if (group.Owner != currentUser)
            {
                //Om användaren som bjöd in inte äger gruppen skickas ett misslyckande från metoden.
                _logger.LogInformation("Failed to add user as only the owner of the group can add new users.");
                return new AddUserToGroupResult(false, "Only the owner of a group can add a user", null);
            }
            group.Users.Add(userToAdd);
            await _context.SaveChangesAsync();
            return new AddUserToGroupResult(true, "", group);

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR ChatService > AddUserToGroup :: {ex.Message}");
            return new AddUserToGroupResult(false, ex.Message, null);
        }
    }
    public async Task<StartGroupResult> StartGroup(string groupName, string currentUser)
    {
        try
        {
            var groupExists = await _context.Groups.SingleOrDefaultAsync(g => g.Name == groupName);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == currentUser);
            if (groupExists != null)
            {
                _logger.LogInformation($"Failed creating new group: group by that name already exists.");
                return new StartGroupResult(false, "Failed creating new group: group by that name already exists");

            }
            if (user == null)
            {
                _logger.LogInformation($"Failed creating new group: user does not exist.");
                return new StartGroupResult(false, "Failed creating new group: user does not exist");

            }
            var group = new Group { Name = groupName, Owner = currentUser };
            group.Users.Add(user);
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            return new StartGroupResult(true, "");

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR ChatService > StartGroup :: {ex.Message}");
            return new StartGroupResult(false, ex.Message);
        }
    }
    public async Task<StartPrivateChatResult> StartPrivateChat(string target, string currentUser)
    {
        var targetUser = await _context.Users.SingleOrDefaultAsync(u => u.Username == target);
        if (targetUser == null)
        {
            _logger.LogInformation($"{target} could not be found.");
            return new StartPrivateChatResult(false, $"{target} could not be found.", null, null);
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
        var payload = new PrivateChatPayload(messages, conversation.Id, conversation.Participant1, conversation.Participant2);
        return new StartPrivateChatResult(true, "", conversation.Id, payload);

    }
}
