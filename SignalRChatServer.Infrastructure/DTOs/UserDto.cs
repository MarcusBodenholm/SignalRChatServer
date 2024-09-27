namespace SignalRChatServer.Infrastructure.DTOs;

public class UserDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
}
