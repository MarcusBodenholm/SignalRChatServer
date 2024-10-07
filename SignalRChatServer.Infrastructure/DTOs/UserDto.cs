namespace SignalRChatServer.Infrastructure.DTOs;

public class UserDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
}

public class ChatMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
}
public class GroupMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required string Room { get; set; }
    public required DateTime TimeStamp { get; set; }


}
public class PrivateMessageDto
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public required DateTime TimeStamp { get; set; }

}
public class PrivateChatDto
{
    public string Participant1 { get; set; }
    public string Participant2 { get; set; }
    public Guid Id { get; set; }
}
public class GroupDto
{
    public string Name { get; set; }
    public string Owner {  get; set; }
}
