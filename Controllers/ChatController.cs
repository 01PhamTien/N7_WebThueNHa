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
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".doc", ".docx" };
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public ChatController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
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


    /// <summary>Tải tệp đính kèm vào hội thoại (tối đa 10 MB, chỉ nhận ảnh/PDF/Word).</summary>
    [HttpPost("conversations/{conversationId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(
        Guid conversationId,
        [FromForm] IFormFile file,
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

        var contentType = file.ContentType.ToLowerInvariant();
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        if (!allowedContentTypes.Contains(contentType))
            return BadRequest(new { message = "Kiểu nội dung tệp không được hỗ trợ." });

        var safeOriginalName = Path.GetFileName(file.FileName);
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
                    FileUrl = Path.Combine(relativeFolder, storedName).Replace('\\\\', '/'),
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
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(absolutePath);
            throw;
        }

        var attachment = message.Attachments.Single();
        return StatusCode(StatusCodes.Status201Created, new
        {
            messageId = message.Id,
            attachmentId = attachment.Id,
            fileName = attachment.FileName,
            fileSize = attachment.FileSize,
            contentType = attachment.ContentType,
            downloadUrl = $"/api/chat/attachments/{attachment.Id}"
        });
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

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
