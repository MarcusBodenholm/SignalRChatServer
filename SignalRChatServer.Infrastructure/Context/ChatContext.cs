using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRChatServer.Infrastructure.Context;

public class ChatContext : DbContext
{
    public DbSet<User> Users { get; set; }
}
