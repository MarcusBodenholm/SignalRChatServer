
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.API.Configurations;
using SignalRChatServer.API.Hubs;
using SignalRChatServer.Infrastructure.Context;

namespace SignalRChatServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddSignalR();
        builder.Services.AddControllers();
        builder.Services.ConfigureAuthentication();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddDbContext<ChatContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("ChatDbConnection"));
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5274);
            options.ListenAnyIP(7152, listenOptions =>
            {
                listenOptions.UseHttps();
            });
        });


        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chathub");
        app.MapControllers();

        app.Run();
    }
}
