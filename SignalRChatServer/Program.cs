
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
        builder.Services.ConfigureAuthentication(builder.Configuration);
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddDbContext<ChatContext>(options =>
        {
#if DEBUG 
            options.UseSqlServer(builder.Configuration["ChatDbConnection"]);
#else
            options.UseSqlServer(Environment.GetEnvironmentVariable("ChatDbConnection"));
#endif
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("reactClient", builder =>
            {
                builder.WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            });
        });
        builder.Services.ConfigureServices();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5065);
            options.ListenAnyIP(7174, listenOptions =>
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
        app.UseCors("reactClient");
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chathub");
        app.MapControllers();

        app.Run();
    }
}
