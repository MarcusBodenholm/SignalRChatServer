using AutoFixture;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NSubstitute;
using SignalRChatServer.API.Hubs;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Records;
using SignalRChatServer.Infrastructure.Services;
using System.Security.Claims;
using System.Security.Principal;

namespace SignalRChatServer.Tests.UnitTests.API.Hubs;

public class ChatHubTests
{
    [Fact]
    public async Task HubsAreMockableViaDynamic()
    {
        var mockChatService = Substitute.For<IChatService>();
        var mockChatInMemory = new ChatInMemory();
        var mockLogger = NullLogger<ChatHub>.Instance;
        var mockClients = Substitute.For<IHubCallerClients>();
        var mockCaller = Substitute.For<ISingleClientProxy>();

        var mockHubCallerContext = Substitute.For<HubCallerContext>();
        var identity = Substitute.For<IIdentity>();
        identity.IsAuthenticated.Returns(true);
        identity.Name.Returns("TestUser");
        
        var claimsPrincipal = Substitute.For<ClaimsPrincipal>();
        claimsPrincipal.Identity.Returns(identity);

        mockHubCallerContext.User.Returns(claimsPrincipal);

        mockClients.Caller.Returns(mockCaller);

        var hub = new ChatHub(mockChatService, mockChatInMemory, mockLogger) { Clients = mockClients, Context = mockHubCallerContext };

        await hub.SendMessage("Hello, world!");

        await mockCaller.Received(1).SendCoreAsync("ReceiveMessage", Arg.Is<object[]>(o => o.Length == 1 && (string)o[0] == "TestUser: Hello, world!"), 
            default);

    }
    [Fact]
    public async Task StartGroup_ShouldHandleCorrectly_WhenGivenValidGroupName()
    {
        //Arrange
        var mockChatService = Substitute.For<IChatService>();
        var mockChatInMemory = new ChatInMemory();
        var mockLogger = NullLogger<ChatHub>.Instance;
        var mockClients = Substitute.For<IHubCallerClients>();
        var mockCaller = Substitute.For<ISingleClientProxy>();

        var mockHubCallerContext = Substitute.For<HubCallerContext>();
        var identity = Substitute.For<IIdentity>();
        identity.IsAuthenticated.Returns(true);
        identity.Name.Returns("TestUser");

        var claimsPrincipal = Substitute.For<ClaimsPrincipal>();
        claimsPrincipal.Identity.Returns(identity);

        var groupName = "NewGroup";
        mockHubCallerContext.User.Returns(claimsPrincipal);
        var startGroupResult = new StartGroupResult(true, "");
        mockChatService.StartGroup(groupName, "TestUser").Returns(startGroupResult);
        var hub = new ChatHub(mockChatService, mockChatInMemory, mockLogger) { Clients = mockClients, Context = mockHubCallerContext };


        //Act
        await hub.StartGroup(groupName);
        //Assert
        await mockCaller.Received(0).SendCoreAsync("ReceiveError", Arg.Is<object[]>(o => o.Length == 1 && (string)o[0] == "System"),
            default);
    }
    [Fact]
    public async Task SendGroupMessage_ShouldHandleCorrectly_GivenCorrectInput()
    {
        //Arrange
        var mockChatService = Substitute.For<IChatService>();
        var mockChatInMemory = new ChatInMemory();
        var mockLogger = NullLogger<ChatHub>.Instance;
        var mockClients = Substitute.For<IHubCallerClients>();
        var mockCaller = Substitute.For<ISingleClientProxy>();

        var mockHubCallerContext = Substitute.For<HubCallerContext>();
        var identity = Substitute.For<IIdentity>();
        identity.IsAuthenticated.Returns(true);
        identity.Name.Returns("TestUser");

        var claimsPrincipal = Substitute.For<ClaimsPrincipal>();
        claimsPrincipal.Identity.Returns(identity);
        var message = "Hello world!";
        var groupName = "NewGroup";
        mockHubCallerContext.User.Returns(claimsPrincipal);
        mockClients.Group(groupName).Returns(mockCaller);

        var groupMessageDto = new GroupMessageDto { Message = message, Room = groupName, TimeStamp = DateTime.Now, Username = "TestUser" };
        var saveGroupMessageResult = new SaveGroupMessageResult(true, message, groupMessageDto);
        
        mockChatService.SaveGroupMessage(message, groupName, "TestUser").Returns(saveGroupMessageResult);
        var hub = new ChatHub(mockChatService, mockChatInMemory, mockLogger) { Clients = mockClients, Context = mockHubCallerContext };


        //Act
        await hub.SendGroupMessage(message,groupName);
        //Assert
        await mockCaller.Received(1).SendCoreAsync("ReceiveGroupMessage", Arg.Is<object[]>(o => o.Length == 1 && o[0] == saveGroupMessageResult.Dto),
            default);
    }
    [Fact]
    public async Task SendPrivateMessage_ShouldHandleCorrectly_GivenCorrectInput()
    {
        //Arrange
        var fixture = new Fixture();
        var mockChatService = Substitute.For<IChatService>();
        var mockChatInMemory = new ChatInMemory();
        var mockLogger = NullLogger<ChatHub>.Instance;
        var mockClients = Substitute.For<IHubCallerClients>();
        var mockCaller = Substitute.For<ISingleClientProxy>();

        var conversationId = Guid.NewGuid();


        var mockHubCallerContext = Substitute.For<HubCallerContext>();
        var identity = Substitute.For<IIdentity>();
        identity.IsAuthenticated.Returns(true);
        identity.Name.Returns("TestUser");

        var claimsPrincipal = Substitute.For<ClaimsPrincipal>();
        claimsPrincipal.Identity.Returns(identity);
        var message = "Hello world!";
        mockHubCallerContext.User.Returns(claimsPrincipal);
        mockClients.Group(conversationId.ToString()).Returns(mockCaller);

        var chatMessage = new ChatMessage { Message = message, User = new User { PasswordHash = "121", Username = "TestUser" }, Timestamp = DateTime.Now };
        var sendPrivateMessageResult = new SendPrivateMessageResult(true, message, message, chatMessage);

        mockChatService.SendPrivateMessage(message, conversationId, "TestUser").Returns(sendPrivateMessageResult);
        var hub = new ChatHub(mockChatService, mockChatInMemory, mockLogger) { Clients = mockClients, Context = mockHubCallerContext };

        var sentObject = new {Username = "TestUser", message = message, Timestamp = chatMessage.Timestamp};

        //Act
        await hub.SendPrivateMessage(conversationId, message);
        //Assert
        await mockCaller.Received(1).SendCoreAsync("ReceivePrivateMessage", Arg.Is<object[]>(o => o.Length == 1),
            default);

    }


}
