using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRChatServer.Infrastructure.Models;
public class ChatMessage
{
    public int Id { get; set; }
    public required User User { get; set; }
    public required string Message { get; set; }
    public required Group Group { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
