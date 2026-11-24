using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Chat;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChatController(AppDbContext db) => _db = db;

    /// <summary>Tạo hoặc lấy hội thoại riêng của người thuê với chủ nhà trên một căn nhà.</summary>
    [HttpPost("properties/{propertyId:guid}/conversation")]
    public async Task<IActionResult> GetOrCreateConversation(Guid propertyId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var property = await _db.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        if (property.LandlordId == currentUserId.Value)
            return BadRequest(new { message = "Chủ nhà không thể tự tạo cuộc trò chuyện với chính mình." });

        if (property.ListingStatus != PropertyListingStatus.Published ||
            property.ModerationStatus != PropertyModerationStatus.Approved)
            return Conflict(new { message = "Căn nhà chưa được duyệt hoặc hiện không được đăng công khai." });

        var existing = await _db.Conversations
            .Include(x => x.Participants)
            .SingleOrDefaultAsync(x =>
                x.PropertyId == propertyId &&
                x.TenantId == currentUserId.Value &&
                x.LandlordId == property.LandlordId,
                cancellationToken);

        if (existing is not null)
            return Ok(new { conversationId = existing.Id, created = false });

        var conversation = new Conversation
        {
            PropertyId = propertyId,
            TenantId = currentUserId.Value,
            LandlordId = property.LandlordId,
            CreatedById = currentUserId.Value,
            Participants =
            [
                new ConversationParticipant { UserId = currentUserId.Value },
                new ConversationParticipant { UserId = property.LandlordId }
            ]
        };

        _db.Conversations.Add(conversation);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai request đồng thời có thể cùng tạo hội thoại; unique index đảm bảo chỉ có một bản ghi.
            var raced = await _db.Conversations.AsNoTracking().SingleOrDefaultAsync(x =>
                x.PropertyId == propertyId &&
                x.TenantId == currentUserId.Value &&
                x.LandlordId == property.LandlordId,
                cancellationToken);

            if (raced is not null)
                return Ok(new { conversationId = raced.Id, created = false });

            throw;
        }

        return CreatedAtAction(nameof(GetMessages), new { conversationId = conversation.Id }, new
        {
            conversationId = conversation.Id,
            created = true
        });
    }

    /// <summary>Danh sách hội thoại của tài khoản đang đăng nhập.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var items = await _db.ConversationParticipants
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId.Value)
            .OrderByDescending(x => x.Conversation.LastMessageAt ?? x.Conversation.CreatedAt)
            .Select(x => new ConversationListItem(
                x.Conversation.Id,
                x.Conversation.PropertyId,
                x.Conversation.Property.Title,
                x.Conversation.TenantId,
                x.Conversation.Tenant.FullName,
                x.Conversation.LandlordId,
                x.Conversation.Landlord.FullName,
                x.Conversation.Messages
                    .Where(m => m.DeletedAt == null)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                x.Conversation.LastMessageAt,
                x.Conversation.CreatedAt,
                x.IsArchived))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Lấy lịch sử tin nhắn theo trang; chỉ thành viên hội thoại được truy cập.</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var isParticipant = await _db.ConversationParticipants.AnyAsync(
            x => x.ConversationId == conversationId && x.UserId == currentUserId.Value,
            cancellationToken);
        if (!isParticipant)
            return NotFound(new { message = "Không tìm thấy hội thoại hoặc bạn không có quyền truy cập." });

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Messages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.DeletedAt == null);

        var totalItems = await query.CountAsync(cancellationToken);
        var messages = await query
            .OrderByDescending(x => x.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ChatMessageItem(
                x.Id,
                x.SenderId,
                x.Sender.FullName,
                x.Content,
                x.MessageType.ToString(),
                x.SentAt,
                x.EditedAt,
                x.SenderId == currentUserId.Value))
            .ToListAsync(cancellationToken);

        messages.Reverse();

        return Ok(new PagedChatMessages(
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize),
            messages));
    }

    /// <summary>Gửi tin nhắn văn bản; người gửi bắt buộc là thành viên hội thoại.</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var participant = await _db.ConversationParticipants
            .Include(x => x.Conversation)
            .SingleOrDefaultAsync(
                x => x.ConversationId == conversationId && x.UserId == currentUserId.Value,
                cancellationToken);

        if (participant is null)
            return NotFound(new { message = "Không tìm thấy hội thoại hoặc bạn không có quyền gửi tin." });

        var content = request.Content.Trim();
        if (content.Length == 0)
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống." });

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = currentUserId.Value,
            Content = content,
            MessageType = MessageType.Text,
            SentAt = DateTime.UtcNow
        };

        participant.Conversation.LastMessageAt = message.SentAt;
        participant.LastReadAt = message.SentAt;

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            messageId = message.Id,
            conversationId,
            senderId = message.SenderId,
            content = message.Content,
            sentAt = message.SentAt
        });
    }

    /// <summary>Đánh dấu hội thoại đã đọc đến thời điểm hiện tại.</summary>
    [HttpPatch("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var participant = await _db.ConversationParticipants.SingleOrDefaultAsync(
            x => x.ConversationId == conversationId && x.UserId == currentUserId.Value,
            cancellationToken);

        if (participant is null)
            return NotFound(new { message = "Không tìm thấy hội thoại hoặc bạn không có quyền truy cập." });

        participant.LastReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Ẩn/hiện hội thoại khỏi danh sách cá nhân, không xóa dữ liệu hội thoại.</summary>
    [HttpPatch("conversations/{conversationId:guid}/archive")]
    public async Task<IActionResult> ArchiveConversation(
        Guid conversationId,
        ArchiveConversationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var participant = await _db.ConversationParticipants.SingleOrDefaultAsync(
            x => x.ConversationId == conversationId && x.UserId == currentUserId.Value,
            cancellationToken);

        if (participant is null)
            return NotFound(new { message = "Không tìm thấy hội thoại hoặc bạn không có quyền truy cập." });

        participant.IsArchived = request.IsArchived;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { conversationId, isArchived = participant.IsArchived });
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
