using SignalRChatServer.Infrastructure.Models;

namespace SignalRChatServer.Infrastructure.Services;
public interface IAuthServices
{
    string GenerateJwtToken(User user, string secretKey);
}