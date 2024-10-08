using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRChatServer.Infrastructure.Records;

public readonly record struct DeleteGroupResult(bool Success, string Message, List<string>? Users);
public readonly record struct SaveGroupMessageResult(bool Success, string Message, GroupMessageDto? Dto);
public readonly record struct AddUserToGroupResult(bool Success, string Message, Group? Group);
public readonly record struct StartGroupResult(bool Success, string Message);
public readonly record struct SendPrivateMessageResult(bool Success, string Message, string PrivateMessage, ChatMessage? ChatMessage);
public readonly record struct StartPrivateChatResult(bool Success, string Message, Guid? ConversationId, PrivateChatPayload? Payload);
public readonly record struct PrivateChatPayload(List<PrivateMessageDto> messages, Guid Id, string Participant1, string Participant2);