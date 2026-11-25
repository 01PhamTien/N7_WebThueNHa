using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Web2_ThueNha.Data;

/// <summary>
/// Tạo dữ liệu demo có thể chạy lặp lại, chỉ được gọi trong môi trường Development.
/// Không tạo ảnh giả để người dùng tự bổ sung hình ảnh sau.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoPassword = "NhaO@123456";
    private const string AdminEmail = "admin@nhao.test";

    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        // Một tài khoản đánh dấu giúp seed idempotent. Toàn bộ quá trình nằm trong transaction.
        if (await db.Users.AnyAsync(x => x.Email == AdminEmail, cancellationToken))
            return;

        var roles = await db.Roles.ToDictionaryAsync(x => x.Name, cancellationToken);
        var propertyTypes = await db.PropertyTypes.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var amenities = await db.Amenities.OrderBy(x => x.Id).ToListAsync(cancellationToken);

        if (!roles.ContainsKey("Admin") || !roles.ContainsKey("Landlord") || !roles.ContainsKey("Tenant"))
            throw new InvalidOperationException("Thiếu dữ liệu vai trò. Hãy chạy migration trước khi seed demo.");
        if (propertyTypes.Count == 0 || amenities.Count == 0)
            throw new InvalidOperationException("Thiếu danh mục loại nhà hoặc tiện ích. Hãy chạy migration trước khi seed demo.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var admin = CreateUser(0x10000001, 1, "Quản trị NhàỞ", AdminEmail, "0900000001", roles["Admin"].Id, passwordHasher, now);
        var landlordNames = new[] { "Nguyễn Minh Anh", "Trần Quốc Bảo", "Lê Hoàng Nam", "Phạm Thu Hà", "Võ Thành Đạt" };
        var landlords = landlordNames.Select((name, index) =>
            CreateUser(0x10000002, index + 1, name, "chunha" + (index + 1).ToString("00") + "@nhao.test",
                "09100000" + (index + 1).ToString("00"), roles["Landlord"].Id, passwordHasher, now.AddDays(-120 + index))).ToList();

        var tenantNames = new[]
        {
            "Nguyễn Hoài An", "Trần Gia Huy", "Lê Khánh Linh", "Phạm Đức Anh", "Vũ Ngọc Mai",
            "Đặng Minh Khang", "Bùi Thanh Trúc", "Đỗ Nhật Nam", "Hồ Bảo Ngọc", "Phan Tuấn Kiệt",
            "Nguyễn Thảo Vy", "Trương Quang Huy", "Lý Minh Châu", "Dương Quốc Khánh", "Mai Phương Thảo",
            "Cao Anh Tú", "Tạ Ngọc Hân", "Đinh Thành Công"
        };
        var tenants = tenantNames.Select((name, index) =>
            CreateUser(0x10000003, index + 1, name, "nguoithue" + (index + 1).ToString("00") + "@nhao.test",
                "09200000" + (index + 1).ToString("00"), roles["Tenant"].Id, passwordHasher, now.AddDays(-90 + index))).ToList();

        db.Users.AddRange(new[] { admin }.Concat(landlords).Concat(tenants));
        await db.SaveChangesAsync(cancellationToken);

        var cities = new[]
        {
            ("Thành phố Huế", "Vỹ Dạ", "Nguyễn Sinh Cung"),
            ("Đà Nẵng", "Mỹ An", "Ngũ Hành Sơn"),
            ("Hà Nội", "Dịch Vọng", "Cầu Giấy"),
            ("TP. Hồ Chí Minh", "Thảo Điền", "Thủ Đức"),
            ("Cần Thơ", "An Khánh", "Ninh Kiều"),
            ("Bình Dương", "Phú Hòa", "Thủ Dầu Một")
        };
        var propertyTitles = new[]
        {
            "Phòng trọ thoáng mát gần trung tâm",
            "Căn hộ mini đầy đủ nội thất",
            "Studio riêng tư có ban công",
            "Nhà nguyên căn phù hợp gia đình",
            "Phòng cho thuê gần trường đại học",
            "Căn hộ hiện đại có thang máy",
            "Phòng rộng cửa sổ lớn, nhiều ánh sáng",
            "Mặt bằng tầng trệt thuận tiện kinh doanh",
            "Căn hộ 1 phòng ngủ khu dân cư yên tĩnh"
        };
        var descriptions = new[]
        {
            "Không gian sạch sẽ, thông thoáng, khu vực an ninh và thuận tiện di chuyển. Có thể xem nhà trực tiếp theo lịch hẹn.",
            "Nhà được bố trí gọn gàng, khu vực sinh hoạt riêng tư, gần cửa hàng tiện lợi và các tuyến đường chính.",
            "Phù hợp người đi làm hoặc sinh viên. Chủ nhà hỗ trợ trao đổi lịch xem và điều kiện thuê rõ ràng."
        };
        var prices = new[] { 1800000m, 2200000m, 2800000m, 3200000m, 3800000m, 4500000m, 5200000m, 6500000m, 7800000m, 9500000m };
        var properties = new List<Property>();

        for (var i = 0; i < 45; i++)
        {
            var city = cities[i % cities.Length];
            var state = i % 10;
            var moderation = state <= 6
                ? PropertyModerationStatus.Approved
                : state == 7
                    ? PropertyModerationStatus.Pending
                    : state == 8
                        ? PropertyModerationStatus.Rejected
                        : PropertyModerationStatus.Approved;
            var listing = state <= 6
                ? PropertyListingStatus.Published
                : state == 7
                    ? PropertyListingStatus.Draft
                    : state == 8
                        ? PropertyListingStatus.Hidden
                        : PropertyListingStatus.Closed;

            properties.Add(new Property
            {
                Id = DemoId(0x20000001, i + 1),
                LandlordId = landlords[i % landlords.Count].Id,
                PropertyTypeId = propertyTypes[i % propertyTypes.Count].Id,
                Title = propertyTitles[i % propertyTitles.Length] + " #" + (i + 1).ToString("00"),
                Description = descriptions[i % descriptions.Length],
                PricePerMonth = prices[i % prices.Length],
                Area = 18 + (i % 9) * 4,
                Bedrooms = i % 4,
                Bathrooms = i % 2 + 1,
                AddressLine = (12 + i).ToString() + " " + city.Item3,
                WardName = city.Item2,
                ProvinceName = city.Item1,
                ModerationStatus = moderation,
                ListingStatus = listing,
                CreatedAt = now.AddDays(-75 + i)
            });
        }

        db.Properties.AddRange(properties);
        await db.SaveChangesAsync(cancellationToken);

        var propertyAmenities = properties.SelectMany((property, index) =>
            Enumerable.Range(0, 3).Select(offset => new PropertyAmenity
            {
                PropertyId = property.Id,
                AmenityId = amenities[(index + offset * 2) % amenities.Count].Id
            })).ToList();
        db.PropertyAmenities.AddRange(propertyAmenities);

        var publishedProperties = properties
            .Where(x => x.ModerationStatus == PropertyModerationStatus.Approved
                && x.ListingStatus == PropertyListingStatus.Published)
            .ToList();

        var bookings = new List<Booking>();
        for (var i = 0; i < 36; i++)
        {
            var status = i < 15
                ? BookingStatus.Accepted
                : i < 24
                    ? BookingStatus.Pending
                    : i < 28
                        ? BookingStatus.Rejected
                        : i < 32
                            ? BookingStatus.Cancelled
                            : BookingStatus.Expired;

            var tenant = tenants[i % tenants.Count];
            var property = publishedProperties[i % publishedProperties.Count];
            var contractStatus = i < 7
                ? RentalContractStatus.Active
                : i < 10
                    ? RentalContractStatus.AwaitingSignature
                    : i < 12
                        ? RentalContractStatus.Draft
                        : i < 14
                            ? RentalContractStatus.Completed
                            : RentalContractStatus.Terminated;

            DateOnly startDate;
            DateOnly endDate;
            if (status == BookingStatus.Accepted)
            {
                startDate = contractStatus switch
                {
                    RentalContractStatus.Completed => today.AddMonths(-9),
                    RentalContractStatus.Terminated => today.AddMonths(-5),
                    RentalContractStatus.Active => today.AddMonths(-1),
                    RentalContractStatus.AwaitingSignature => today.AddDays(7),
                    _ => today.AddDays(14)
                };
                endDate = contractStatus switch
                {
                    RentalContractStatus.Completed => today.AddMonths(-3),
                    RentalContractStatus.Terminated => today.AddMonths(-1),
                    _ => startDate.AddMonths(12)
                };
            }
            else
            {
                startDate = today.AddDays(14 + i);
                endDate = startDate.AddMonths(6);
            }

            bookings.Add(new Booking
            {
                Id = DemoId(0x30000001, i + 1),
                PropertyId = property.Id,
                TenantId = tenant.Id,
                StartDate = startDate,
                EndDate = endDate,
                NumberOfPeople = i % 3 + 1,
                ProposedPrice = property.PricePerMonth,
                Message = "Mình quan tâm căn nhà này, mong chủ nhà trao đổi thêm về lịch xem và điều kiện thuê.",
                Status = status,
                RespondedAt = status == BookingStatus.Pending ? null : now.AddDays(-20 + i),
                RejectionReason = status == BookingStatus.Rejected ? "Thời gian thuê chưa phù hợp với kế hoạch của chủ nhà." : null,
                ExpiresAt = status == BookingStatus.Pending ? now.AddDays(7) : null,
                CreatedAt = now.AddDays(-35 + i)
            });
        }

        db.Bookings.AddRange(bookings);
        await db.SaveChangesAsync(cancellationToken);

        var acceptedBookings = bookings.Where(x => x.Status == BookingStatus.Accepted).ToList();
        var contracts = new List<RentalContract>();
        for (var i = 0; i < acceptedBookings.Count; i++)
        {
            var booking = acceptedBookings[i];
            var property = properties.Single(x => x.Id == booking.PropertyId);
            var contractStatus = i < 7
                ? RentalContractStatus.Active
                : i < 10
                    ? RentalContractStatus.AwaitingSignature
                    : i < 12
                        ? RentalContractStatus.Draft
                        : i < 14
                            ? RentalContractStatus.Completed
                            : RentalContractStatus.Terminated;
            var landlordSigned = contractStatus is RentalContractStatus.Active
                or RentalContractStatus.AwaitingSignature
                or RentalContractStatus.Completed
                or RentalContractStatus.Terminated;
            var tenantSigned = contractStatus is RentalContractStatus.Active
                or RentalContractStatus.Completed
                or RentalContractStatus.Terminated;

            contracts.Add(new RentalContract
            {
                Id = DemoId(0x40000001, i + 1),
                BookingId = booking.Id,
                PropertyId = property.Id,
                LandlordId = property.LandlordId,
                TenantId = booking.TenantId,
                ContractCode = "DEMO-HD-" + (i + 1).ToString("000"),
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                MonthlyRent = property.PricePerMonth,
                DepositAmount = property.PricePerMonth,
                PaymentDueDay = 5 + i % 20,
                Terms = "Dữ liệu hợp đồng mẫu để kiểm thử luồng xem, ký và quản lý hợp đồng. Không phải hợp đồng giao dịch thật.",
                Status = contractStatus,
                LandlordSignedAt = landlordSigned ? now.AddDays(-12 + i) : null,
                TenantSignedAt = tenantSigned ? now.AddDays(-10 + i) : null,
                SignedAt = tenantSigned ? now.AddDays(-10 + i) : null,
                CreatedAt = now.AddDays(-18 + i)
            });
        }

        db.RentalContracts.AddRange(contracts);
        await db.SaveChangesAsync(cancellationToken);

        var invoices = new List<Invoice>();
        var invoiceNumber = 1;
        foreach (var (contract, index) in contracts.Select((value, index) => (value, index)))
        {
            var depositStatus = contract.Status switch
            {
                RentalContractStatus.Draft => InvoiceStatus.Draft,
                RentalContractStatus.AwaitingSignature => InvoiceStatus.Issued,
                _ => InvoiceStatus.Paid
            };
            var deposit = new Invoice
            {
                Id = DemoId(0x50000001, invoiceNumber),
                ContractId = contract.Id,
                InvoiceCode = "DEMO-HD-" + invoiceNumber.ToString("0000"),
                InvoiceType = InvoiceType.Deposit,
                IssueDate = today.AddDays(-20),
                DueDate = today.AddDays(-10),
                TotalAmount = contract.DepositAmount,
                Status = depositStatus,
                CreatedAt = now.AddDays(-18 + index),
                Items = new List<InvoiceItem>
                {
                    new() { Description = "Tiền đặt cọc", Quantity = 1, UnitPrice = contract.DepositAmount, Amount = contract.DepositAmount }
                }
            };
            invoices.Add(deposit);
            invoiceNumber++;

            var firstPeriod = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
            for (var month = 0; month < 2; month++)
            {
                var periodStart = firstPeriod.AddMonths(month);
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                var monthlyStatus = contract.Status == RentalContractStatus.Draft
                    ? InvoiceStatus.Draft
                    : ((index + month) % 4) switch
                    {
                        0 => InvoiceStatus.Paid,
                        1 => InvoiceStatus.PartiallyPaid,
                        2 => InvoiceStatus.Issued,
                        _ => InvoiceStatus.Overdue
                    };
                var utilities = 280000m + (index % 4) * 65000m;
                var total = contract.MonthlyRent + utilities;
                invoices.Add(new Invoice
                {
                    Id = DemoId(0x50000001, invoiceNumber),
                    ContractId = contract.Id,
                    InvoiceCode = "DEMO-HD-" + invoiceNumber.ToString("0000"),
                    InvoiceType = InvoiceType.MonthlyRent,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    IssueDate = periodStart.AddDays(-5),
                    DueDate = periodStart.AddDays(5),
                    TotalAmount = total,
                    Status = monthlyStatus,
                    CreatedAt = now.AddDays(-12 + index + month),
                    Items = new List<InvoiceItem>
                    {
                        new() { Description = "Tiền thuê nhà", Quantity = 1, UnitPrice = contract.MonthlyRent, Amount = contract.MonthlyRent },
                        new() { Description = "Điện, nước và dịch vụ", Quantity = 1, UnitPrice = utilities, Amount = utilities }
                    }
                });
                invoiceNumber++;
            }
        }

        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync(cancellationToken);

        var payments = new List<Payment>();
        var paymentNumber = 1;
        foreach (var (invoice, index) in invoices.Select((value, index) => (value, index)))
        {
            PaymentStatus? status = invoice.Status switch
            {
                InvoiceStatus.Paid => PaymentStatus.Succeeded,
                InvoiceStatus.PartiallyPaid => PaymentStatus.Succeeded,
                InvoiceStatus.Overdue => PaymentStatus.Failed,
                InvoiceStatus.Issued when index % 3 == 0 => PaymentStatus.Pending,
                _ => null
            };
            if (status is null)
                continue;

            var contract = contracts.Single(x => x.Id == invoice.ContractId);
            var amount = invoice.Status == InvoiceStatus.PartiallyPaid
                ? decimal.Round(invoice.TotalAmount / 2, 0)
                : invoice.TotalAmount;
            payments.Add(new Payment
            {
                Id = DemoId(0x60000001, paymentNumber),
                InvoiceId = invoice.Id,
                PayerId = contract.TenantId,
                Amount = amount,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaymentProvider = "Demo",
                TransactionCode = status == PaymentStatus.Succeeded ? "DEMO-TXN-" + paymentNumber.ToString("0000") : null,
                Status = status.Value,
                PaidAt = status == PaymentStatus.Succeeded ? now.AddDays(-5 + index % 4) : null,
                FailureReason = status == PaymentStatus.Failed ? "Giao dịch mẫu thất bại để kiểm thử trạng thái thanh toán." : null,
                CreatedAt = now.AddDays(-7 + index % 5)
            });
            paymentNumber++;
        }

        db.Payments.AddRange(payments);
        await db.SaveChangesAsync(cancellationToken);

        var conversations = new List<Conversation>();
        var participants = new List<ConversationParticipant>();
        var messages = new List<Message>();
        var messageSamples = new[]
        {
            "Chào anh/chị, căn nhà còn trống không ạ?",
            "Dạ căn này hiện vẫn còn, bạn muốn xem nhà vào thời gian nào?",
            "Mình có thể ghé xem vào chiều mai được không?",
            "Được bạn nhé, mình sẽ sắp xếp thời gian.",
            "Cho mình hỏi chi phí điện nước được tính như thế nào?",
            "Chi phí sẽ được ghi rõ trong thỏa thuận thuê nhà.",
            "Cảm ơn anh/chị, mình sẽ xác nhận lại lịch hẹn.",
            "Mình đã nhận được thông tin. Hẹn gặp bạn!"
        };

        foreach (var (booking, index) in bookings.Take(24).Select((value, index) => (value, index)))
        {
            var property = properties.Single(x => x.Id == booking.PropertyId);
            var landlordId = property.LandlordId;
            var createdAt = now.AddDays(-8 + index % 5);
            var conversationId = DemoId(0x70000001, index + 1);
            conversations.Add(new Conversation
            {
                Id = conversationId,
                PropertyId = property.Id,
                TenantId = booking.TenantId,
                LandlordId = landlordId,
                BookingId = booking.Id,
                CreatedById = booking.TenantId,
                LastMessageAt = now.AddHours(-index),
                CreatedAt = createdAt
            });
            participants.Add(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = booking.TenantId,
                JoinedAt = createdAt,
                LastReadAt = now.AddMinutes(-10),
                IsArchived = false
            });
            participants.Add(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = landlordId,
                JoinedAt = createdAt,
                LastReadAt = index % 3 == 0 ? now.AddDays(-2) : now.AddMinutes(-15),
                IsArchived = index % 12 == 0
            });

            for (var messageIndex = 0; messageIndex < messageSamples.Length; messageIndex++)
            {
                messages.Add(new Message
                {
                    Id = DemoId(0x80000001, index * messageSamples.Length + messageIndex + 1),
                    ConversationId = conversationId,
                    SenderId = messageIndex % 2 == 0 ? booking.TenantId : landlordId,
                    Content = messageSamples[messageIndex],
                    MessageType = MessageType.Text,
                    SentAt = createdAt.AddMinutes(messageIndex * 18)
                });
            }
        }

        db.Conversations.AddRange(conversations);
        await db.SaveChangesAsync(cancellationToken);
        db.ConversationParticipants.AddRange(participants);
        db.Messages.AddRange(messages);
        await db.SaveChangesAsync(cancellationToken);

        var favorites = new List<Favorite>();
        for (var tenantIndex = 0; tenantIndex < tenants.Count; tenantIndex++)
        {
            for (var favoriteIndex = 0; favoriteIndex < 4; favoriteIndex++)
            {
                var property = publishedProperties[(tenantIndex * 3 + favoriteIndex * 7) % publishedProperties.Count];
                favorites.Add(new Favorite
                {
                    UserId = tenants[tenantIndex].Id,
                    PropertyId = property.Id,
                    CreatedAt = now.AddDays(-favoriteIndex - tenantIndex)
                });
            }
        }

        var notifications = new List<Notification>();
        var notificationTitles = new[]
        {
            "Có cập nhật mới về yêu cầu thuê",
            "Tin đăng đã được cập nhật",
            "Bạn có hóa đơn cần theo dõi",
            "Bạn có tin nhắn mới",
            "Thông tin hợp đồng đã thay đổi"
        };
        var notificationContents = new[]
        {
            "Hệ thống đã ghi nhận cập nhật mới. Vui lòng mở mục liên quan để xem chi tiết.",
            "Một căn nhà trong danh sách của bạn vừa có thay đổi trạng thái.",
            "Vui lòng kiểm tra hóa đơn và thời hạn thanh toán trong tài khoản.",
            "Bạn có một cuộc trao đổi mới về căn nhà đang quan tâm.",
            "Vui lòng kiểm tra thông tin hợp đồng để tiếp tục các bước cần thiết."
        };
        var allUsers = new[] { admin }.Concat(landlords).Concat(tenants).ToList();
        for (var userIndex = 0; userIndex < allUsers.Count; userIndex++)
        {
            for (var itemIndex = 0; itemIndex < 5; itemIndex++)
            {
                notifications.Add(new Notification
                {
                    Id = DemoId(0x90000001, userIndex * 5 + itemIndex + 1),
                    UserId = allUsers[userIndex].Id,
                    Title = notificationTitles[(userIndex + itemIndex) % notificationTitles.Length],
                    Content = notificationContents[(userIndex + itemIndex) % notificationContents.Length],
                    NotificationType = (NotificationType)(itemIndex % 6),
                    ReferenceId = itemIndex % 2 == 0 ? properties[(userIndex + itemIndex) % properties.Count].Id : null,
                    IsRead = itemIndex > 1 && userIndex % 2 == 0,
                    CreatedAt = now.AddDays(-itemIndex).AddHours(-userIndex)
                });
            }
        }

        var moderationLogs = properties
            .Where(x => x.ModerationStatus is PropertyModerationStatus.Approved or PropertyModerationStatus.Rejected)
            .Take(12)
            .Select((property, index) => new PropertyModerationLog
            {
                Id = DemoId(0xA0000001, index + 1),
                PropertyId = property.Id,
                AdminId = admin.Id,
                Action = property.ModerationStatus == PropertyModerationStatus.Rejected
                    ? ModerationAction.Rejected
                    : ModerationAction.Approved,
                Reason = property.ModerationStatus == PropertyModerationStatus.Rejected
                    ? "Dữ liệu mẫu: cần bổ sung thông tin trước khi đăng."
                    : "Dữ liệu mẫu: tin đăng đã được duyệt.",
                CreatedAt = now.AddDays(-30 + index)
            }).ToList();

        var reviews = contracts
            .Where(x => x.Status == RentalContractStatus.Completed)
            .Select((contract, index) => new Review
            {
                Id = DemoId(0xB0000001, index + 1),
                ContractId = contract.Id,
                Rating = index % 2 == 0 ? 5 : 4,
                Comment = index % 2 == 0
                    ? "Không gian đúng mô tả, chủ nhà hỗ trợ nhiệt tình."
                    : "Trải nghiệm thuê nhà tốt, thông tin rõ ràng.",
                CreatedAt = now.AddDays(-20 + index)
            }).ToList();

        db.Favorites.AddRange(favorites);
        db.Notifications.AddRange(notifications);
        db.PropertyModerationLogs.AddRange(moderationLogs);
        db.Reviews.AddRange(reviews);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static User CreateUser(
        int prefix,
        int index,
        string fullName,
        string email,
        string phoneNumber,
        int roleId,
        IPasswordHasher<User> passwordHasher,
        DateTime createdAt)
    {
        var user = new User
        {
            Id = DemoId(prefix, index),
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            AvatarUrl = null,
            IsActive = true,
            CreatedAt = createdAt
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        return user;
    }

    private static Guid DemoId(uint prefix, int index) =>
        Guid.Parse(prefix.ToString("x8") + "-d3a0-4e00-9000-" + index.ToString("D12"));
}
