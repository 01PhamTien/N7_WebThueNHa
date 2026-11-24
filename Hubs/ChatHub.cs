using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

namespace Web2_ThueNha.Hubs;

/// <summary>Hub realtime; chỉ thành viên hội thoại mới được tham gia group.</summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db) => _db = db;

    public async Task JoinConversation(Guid conversationId)
    {
        var userIdValue = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            throw new HubException("Không xác định được tài khoản.");

        var isParticipant = await _db.ConversationParticipants.AnyAsync(
            x => x.ConversationId == conversationId && x.UserId == userId);

        if (!isParticipant)
            throw new HubException("Bạn không phải thành viên của hội thoại này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(conversationId));
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(conversationId));

    public static string GetGroupName(Guid conversationId) => $"conversation:{conversationId:N}";
}
