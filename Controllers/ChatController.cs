using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Web2_ThueNha.Hubs;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Chat;
using Web2_ThueNha.Models;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".doc", ".docx" };
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly INotificationService _notifications;

    public ChatController(
        AppDbContext db,
        IWebHostEnvironment environment,
        IHubContext<ChatHub> chatHub,
        INotificationService notifications)
    {
        _db = db;
        _environment = environment;
        _chatHub = chatHub;
        _notifications = notifications;
    }

    /// <summary>Tạo hoặc lấy hội thoại riêng của người thuê với chủ nhà trên một căn nhà.</summary>
    [HttpPost("properties/{propertyId:guid}/conversation")]
    [Authorize(Roles = "Tenant")]
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
    public async Task<IActionResult> GetConversations(
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        if (!IsValidPaging(page, pageSize, 50))
            return BadRequest(new { message = "Tham số page phải từ 1, pageSize phải từ 1 đến 50." });

        var query = _db.ConversationParticipants
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId.Value &&
                        (includeArchived || !x.IsArchived));

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<ConversationListItem>()
            : await query
                .OrderByDescending(x => x.Conversation.LastMessageAt ?? x.Conversation.CreatedAt)
                .ThenByDescending(x => x.Conversation.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                        .ThenByDescending(m => m.Id)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    x.Conversation.LastMessageAt,
                    x.Conversation.Messages.Count(m =>
                        m.DeletedAt == null &&
                        m.SenderId != currentUserId.Value &&
                        m.SentAt > (x.LastReadAt ?? DateTime.MinValue)),
                    x.Conversation.CreatedAt,
                    x.IsArchived))
                .ToListAsync(cancellationToken);

        return Ok(new PagedConversations(
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages,
            items));
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

        if (!IsValidPaging(page, pageSize, 100))
            return BadRequest(new { message = "Tham số page phải từ 1, pageSize phải từ 1 đến 100." });

        var query = _db.Messages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.DeletedAt == null);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var messages = page > totalPages
            ? new List<ChatMessageItem>()
            : await query
                .OrderByDescending(x => x.SentAt)
                .ThenByDescending(x => x.Id)
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
                    x.SenderId == currentUserId.Value,
                    x.Attachments.Select(a => new ChatAttachmentItem(
                        a.Id,
                        a.FileName,
                        a.ContentType,
                        a.FileSize,
                        "/api/chat/attachments/" + a.Id)).ToList()))
                .ToListAsync(cancellationToken);

        messages.Reverse();

        return Ok(new PagedChatMessages(
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1,
            page < totalPages,
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

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var recipientId = participant.Conversation.TenantId == currentUserId.Value
            ? participant.Conversation.LandlordId
            : participant.Conversation.TenantId;
        var notificationText = content.Length > 180 ? content[..180] + "…" : content;
        await _notifications.CreateAsync(
            recipientId,
            "Tin nhắn mới",
            notificationText,
            NotificationType.Message,
            message.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var realtimePayload = new
        {
            messageId = message.Id,
            conversationId,
            senderId = message.SenderId,
            senderName = User.Identity?.Name,
            content = message.Content,
            messageType = message.MessageType.ToString(),
            sentAt = message.SentAt
        };
        await _chatHub.Clients.Group(ChatHub.GetGroupName(conversationId))
            .SendAsync("NhanTinNhan", realtimePayload, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, realtimePayload);
    }


    /// <summary>Chỉ người gửi mới được sửa tin nhắn văn bản của chính mình.</summary>
    [HttpPut("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessage(
        Guid conversationId,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var message = await _db.Messages.SingleOrDefaultAsync(
            x => x.Id == messageId &&
                 x.ConversationId == conversationId &&
                 x.SenderId == currentUserId.Value &&
                 x.DeletedAt == null,
            cancellationToken);

        if (message is null)
            return NotFound(new { message = "Không tìm thấy tin nhắn hoặc bạn không có quyền chỉnh sửa." });

        if (message.MessageType != MessageType.Text)
            return Conflict(new { message = "Chỉ tin nhắn văn bản mới được chỉnh sửa." });

        var content = request.Content.Trim();
        if (content.Length == 0)
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống." });

        message.Content = content;
        message.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            messageId = message.Id,
            conversationId,
            senderId = message.SenderId,
            content = message.Content,
            editedAt = message.EditedAt
        };
        await _chatHub.Clients.Group(ChatHub.GetGroupName(conversationId))
            .SendAsync("SuaTinNhan", payload, cancellationToken);

        return Ok(payload);
    }

    /// <summary>Chỉ người gửi mới được xóa mềm tin nhắn của chính mình.</summary>
    [HttpDelete("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var message = await _db.Messages
            .Include(x => x.Conversation)
            .SingleOrDefaultAsync(
                x => x.Id == messageId &&
                     x.ConversationId == conversationId &&
                     x.SenderId == currentUserId.Value &&
                     x.DeletedAt == null,
                cancellationToken);

        if (message is null)
            return NotFound(new { message = "Không tìm thấy tin nhắn hoặc bạn không có quyền xóa." });

        message.DeletedAt = DateTime.UtcNow;
        message.Content = null;

        if (message.Conversation.LastMessageAt == message.SentAt)
        {
            message.Conversation.LastMessageAt = await _db.Messages
                .Where(x => x.ConversationId == conversationId &&
                            x.Id != messageId &&
                            x.DeletedAt == null)
                .OrderByDescending(x => x.SentAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (DateTime?)x.SentAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _chatHub.Clients.Group(ChatHub.GetGroupName(conversationId))
            .SendAsync("XoaTinNhan", new
            {
                messageId,
                conversationId,
                deletedAt = message.DeletedAt
            }, cancellationToken);

        return NoContent();
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


    /// <summary>Tải tệp đính kèm vào hội thoại (tối đa 10 MB, chỉ nhận ảnh/PDF/Word).</summary>
    [HttpPost("conversations/{conversationId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(
        Guid conversationId,
        IFormFile file,
        [FromForm] string? caption,
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
            return NotFound(new { message = "Không tìm thấy hội thoại hoặc bạn không có quyền gửi tệp." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn tệp cần tải lên." });

        if (file.Length > MaxAttachmentBytes)
            return BadRequest(new { message = "Tệp đính kèm không được vượt quá 10 MB." });

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Định dạng tệp không được hỗ trợ." });

        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedContentType = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => null
        };

        if (expectedContentType is null ||
            !string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Phần mở rộng và kiểu nội dung của tệp không khớp." });

        if (!await HasExpectedFileSignatureAsync(file, extension, cancellationToken))
            return BadRequest(new { message = "Nội dung thực tế của tệp không khớp với định dạng khai báo." });

        if (caption?.Length > 4000)
            return BadRequest(new { message = "Chú thích tệp không được vượt quá 4000 ký tự." });

        var safeOriginalName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (safeOriginalName.Length > 255)
            safeOriginalName = safeOriginalName[^255..];

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativeFolder = Path.Combine("PrivateUploads", "chat", conversationId.ToString("N"));
        var absoluteFolder = Path.Combine(_environment.ContentRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absolutePath = Path.Combine(absoluteFolder, storedName);
        await using (var stream = System.IO.File.Create(absolutePath))
            await file.CopyToAsync(stream, cancellationToken);

        var now = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = currentUserId.Value,
            Content = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            MessageType = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? MessageType.Image
                : MessageType.File,
            SentAt = now,
            Attachments =
            [
                new MessageAttachment
                {
                    FileUrl = Path.Combine(relativeFolder, storedName).Replace(Path.DirectorySeparatorChar, '/'),
                    FileName = safeOriginalName,
                    ContentType = contentType,
                    FileSize = file.Length,
                    CreatedAt = now
                }
            ]
        };

        participant.Conversation.LastMessageAt = now;
        participant.LastReadAt = now;
        _db.Messages.Add(message);

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            var recipientId = participant.Conversation.TenantId == currentUserId.Value
                ? participant.Conversation.LandlordId
                : participant.Conversation.TenantId;
            await _notifications.CreateAsync(
                recipientId,
                "Tệp đính kèm mới",
                $"Bạn có tệp đính kèm mới: {safeOriginalName}.",
                NotificationType.Message,
                message.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(absolutePath);
            throw;
        }

        var attachment = message.Attachments.Single();
        var realtimePayload = new
        {
            messageId = message.Id,
            conversationId,
            senderId = message.SenderId,
            senderName = User.Identity?.Name,
            content = message.Content,
            messageType = message.MessageType.ToString(),
            sentAt = message.SentAt,
            attachment = new
            {
                attachmentId = attachment.Id,
                fileName = attachment.FileName,
                fileSize = attachment.FileSize,
                contentType = attachment.ContentType,
                downloadUrl = $"/api/chat/attachments/{attachment.Id}"
            }
        };
        await _chatHub.Clients.Group(ChatHub.GetGroupName(conversationId))
            .SendAsync("NhanTinNhan", realtimePayload, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, realtimePayload);
    }

    /// <summary>Tải tệp đính kèm; chỉ thành viên hội thoại được phép tải.</summary>
    [HttpGet("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        var attachment = await _db.MessageAttachments
            .AsNoTracking()
            .Where(x => x.Id == attachmentId &&
                x.Message.DeletedAt == null &&
                x.Message.Conversation.Participants.Any(p => p.UserId == currentUserId.Value))
            .Select(x => new
            {
                x.FileUrl,
                x.FileName,
                x.ContentType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
            return NotFound(new { message = "Không tìm thấy tệp hoặc bạn không có quyền truy cập." });

        var relativePath = attachment.FileUrl.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath));
        var privateRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "PrivateUploads", "chat"));
        if (!absolutePath.StartsWith(privateRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Đường dẫn tệp không hợp lệ." });

        if (!System.IO.File.Exists(absolutePath))
            return NotFound(new { message = "Tệp không còn tồn tại trên máy chủ." });

        var stream = System.IO.File.OpenRead(absolutePath);
        return File(stream, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    private static async Task<bool> HasExpectedFileSignatureAsync(
        IFormFile file, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => read >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".webp" => read >= 12 &&
                header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' &&
                header[3] == (byte)'F' && header[8] == (byte)'W' && header[9] == (byte)'E' &&
                header[10] == (byte)'B' && header[11] == (byte)'P',
            ".pdf" => read >= 5 &&
                header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' &&
                header[3] == (byte)'F' && header[4] == (byte)'-',
            ".doc" => read >= 8 &&
                header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&
                header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1,
            ".docx" => read >= 4 &&
                header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
            _ => false
        };
    }

    private static bool IsValidPaging(int page, int pageSize, int maxPageSize) =>
        page >= 1 &&
        pageSize >= 1 &&
        pageSize <= maxPageSize &&
        page <= int.MaxValue / pageSize;

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
