using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Services.Payments;

public sealed class PaymentService(AppDbContext db, INotificationService notifications) : IPaymentService
{
    public async Task<ServiceResult<InvoiceDetailResponse>> CreateInvoiceAsync(
        Guid contractId, Guid userId, CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.InvoiceType) ||
            request.IssueDate == default ||
            request.DueDate == default ||
            request.DueDate < request.IssueDate ||
            request.Items is null ||
            request.Items.Count > 20 ||
            (request.InvoiceType == InvoiceType.MonthlyRent && request.Items.Count > 1) ||
            (request.InvoiceType != InvoiceType.MonthlyRent &&
             (request.Items.Count == 0 || request.Items.Any(x =>
                 string.IsNullOrWhiteSpace(x.Description) ||
                 x.Description.Trim().Length > 300 ||
                 x.Quantity <= 0 ||
                 x.UnitPrice <= 0 ||
                 decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero) <= 0))))
            return ServiceResult<InvoiceDetailResponse>.BadRequest(
                "Thông tin hóa đơn hoặc dòng chi tiết không hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return ServiceResult<InvoiceDetailResponse>.NotFound("Không tìm thấy hợp đồng.");

        if (contract.LandlordId != userId)
            return ServiceResult<InvoiceDetailResponse>.Forbidden();

        if (contract.Status != RentalContractStatus.Active)
            return ServiceResult<InvoiceDetailResponse>.Conflict(
                "Chỉ hợp đồng đã được cả hai bên xác nhận mới phát hành hóa đơn.");

        if (request.InvoiceType == InvoiceType.Deposit)
        {
            if (request.PeriodStart.HasValue || request.PeriodEnd.HasValue)
                return ServiceResult<InvoiceDetailResponse>.BadRequest("Hóa đơn tiền cọc không có kỳ thanh toán.");

            if (contract.DepositAmount <= 0)
                return ServiceResult<InvoiceDetailResponse>.Conflict("Hợp đồng không có khoản tiền cọc cần thu.");
        }
        else if (request.InvoiceType == InvoiceType.MonthlyRent)
        {
            if (!request.PeriodStart.HasValue || !request.PeriodEnd.HasValue ||
                request.PeriodEnd <= request.PeriodStart ||
                request.PeriodStart < contract.StartDate ||
                request.PeriodEnd > contract.EndDate)
                return ServiceResult<InvoiceDetailResponse>.BadRequest(
                    "Kỳ hóa đơn tiền thuê phải nằm trong thời hạn hợp đồng.");
        }
        else if (request.PeriodStart.HasValue != request.PeriodEnd.HasValue ||
                 (request.PeriodStart.HasValue && request.PeriodEnd <= request.PeriodStart))
        {
            return ServiceResult<InvoiceDetailResponse>.BadRequest("Khoảng thời gian hóa đơn không hợp lệ.");
        }

        if (request.InvoiceType == InvoiceType.MonthlyRent)
        {
            if (request.Items.Count != 1)
                return ServiceResult<InvoiceDetailResponse>.BadRequest(
                    "Hóa đơn tiền thuê chỉ gồm một dòng tiền thuê; phí phát sinh lập hóa đơn loại Khác.");

            var rent = RentalPricing.Calculate(
                contract.MonthlyRent,
                request.PeriodStart!.Value,
                request.PeriodEnd!.Value);

            if (rent.TotalAmount <= 0)
                return ServiceResult<InvoiceDetailResponse>.BadRequest(
                    "Kỳ thuê không hợp lệ để tính tiền.");

            // Backend tự tạo dòng tiền thuê; không nhận nội dung, số lượng hoặc đơn giá từ trình duyệt.
            request.Items =
            [
                new CreateInvoiceItemRequest
                {
                    Description = "Tiền thuê nhà",
                    Quantity = 1,
                    UnitPrice = rent.TotalAmount
                }
            ];
        }

        var totalAmount = request.Items.Sum(x =>
            decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero));

        if (totalAmount <= 0 || totalAmount > 9999999999999999.99m)
            return ServiceResult<InvoiceDetailResponse>.BadRequest(
                "Tổng hóa đơn phải lớn hơn 0 và không vượt giới hạn decimal(18,2).");

        if (request.InvoiceType == InvoiceType.Deposit && totalAmount != contract.DepositAmount)
            return ServiceResult<InvoiceDetailResponse>.BadRequest(
                "Tổng hóa đơn tiền cọc phải bằng tiền cọc trong hợp đồng.");

        var duplicate = request.InvoiceType switch
        {
            InvoiceType.Deposit => await db.Invoices.AnyAsync(x =>
                x.ContractId == contractId &&
                x.InvoiceType == InvoiceType.Deposit &&
                x.Status != InvoiceStatus.Cancelled, cancellationToken),
            InvoiceType.MonthlyRent => await db.Invoices.AnyAsync(x =>
                x.ContractId == contractId &&
                x.InvoiceType == InvoiceType.MonthlyRent &&
                x.PeriodStart == request.PeriodStart &&
                x.PeriodEnd == request.PeriodEnd &&
                x.Status != InvoiceStatus.Cancelled, cancellationToken),
            _ => false
        };

        if (duplicate)
            return ServiceResult<InvoiceDetailResponse>.Conflict(
                "Hóa đơn cùng loại và kỳ thanh toán đã tồn tại.");

        if (request.InvoiceType == InvoiceType.MonthlyRent)
        {
            var overlapsExistingPeriod = await db.Invoices.AnyAsync(x =>
                x.ContractId == contractId &&
                x.InvoiceType == InvoiceType.MonthlyRent &&
                x.Status != InvoiceStatus.Cancelled &&
                x.PeriodStart.HasValue &&
                x.PeriodEnd.HasValue &&
                x.PeriodStart.Value < request.PeriodEnd!.Value &&
                x.PeriodEnd.Value > request.PeriodStart!.Value,
                cancellationToken);

            if (overlapsExistingPeriod)
                return ServiceResult<InvoiceDetailResponse>.Conflict(
                    "Kỳ tiền thuê bị trùng với một hóa đơn chưa hủy. Vui lòng chọn khoảng ngày chưa được lập hóa đơn.");
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var invoice = new Invoice
        {
            ContractId = contractId,
            InvoiceCode = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            InvoiceType = request.InvoiceType,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            TotalAmount = totalAmount,
            Status = request.DueDate < today
                ? InvoiceStatus.Overdue
                : InvoiceStatus.Issued,
            CreatedAt = now,
            Items = request.Items.Select(x => new InvoiceItem
            {
                Description = x.Description.Trim(),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Amount = decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };

        db.Invoices.Add(invoice);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(
                contract.TenantId,
                "Hóa đơn mới được phát hành",
                $"Bạn có hóa đơn {invoice.InvoiceCode} cần thanh toán.",
                NotificationType.Invoice,
                invoice.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            return ServiceResult<InvoiceDetailResponse>.Conflict(
                "Hóa đơn trùng loại/kỳ thanh toán đã được tạo bởi một request khác.");
        }

        var createdInvoice = await GetInvoiceDetailQuery()
            .SingleAsync(x => x.Id == invoice.Id, cancellationToken);
        return ServiceResult<InvoiceDetailResponse>.Created(ToDetail(createdInvoice));
    }

    public async Task<ServiceResult<PaymentResponse>> CreatePaymentAsync(
        Guid invoiceId, Guid userId, CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.PaymentMethod) ||
            request.PaymentMethod is not (PaymentMethod.Cash or PaymentMethod.BankTransfer) ||
            request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount ||
            (request.TransactionCode?.Trim().Length ?? 0) > 150)
            return ServiceResult<PaymentResponse>.BadRequest(
                "Chỉ hỗ trợ Cash/BankTransfer; số tiền và mã giao dịch phải hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var invoice = await db.Invoices
            .Include(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null || invoice.Contract.TenantId != userId)
            return ServiceResult<PaymentResponse>.NotFound("Không tìm thấy hóa đơn.");

        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            return ServiceResult<PaymentResponse>.Conflict(
                "Hóa đơn không ở trạng thái được phép ghi nhận thanh toán.");

        var reservedAmount = await db.Payments
            .Where(x => x.InvoiceId == invoiceId &&
                (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Succeeded))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (reservedAmount + request.Amount > invoice.TotalAmount)
            return ServiceResult<PaymentResponse>.Conflict(
                "Số tiền yêu cầu vượt quá số dư hóa đơn còn lại.");

        var transactionCode = string.IsNullOrWhiteSpace(request.TransactionCode)
            ? null
            : request.TransactionCode.Trim();

        if (transactionCode is not null &&
            await db.Payments.AnyAsync(x => x.TransactionCode == transactionCode, cancellationToken))
            return ServiceResult<PaymentResponse>.Conflict("Mã giao dịch đã được sử dụng.");

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            PayerId = userId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentProvider = "Manual",
            TransactionCode = transactionCode,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(
                invoice.Contract.LandlordId,
                "Có yêu cầu xác nhận thanh toán",
                $"Người thuê đã gửi yêu cầu thanh toán cho hóa đơn {invoice.InvoiceCode}.",
                NotificationType.Payment,
                payment.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            return ServiceResult<PaymentResponse>.Conflict("Mã giao dịch đã được ghi nhận.");
        }

        return ServiceResult<PaymentResponse>.Created(ToResponse(payment));
    }

    public async Task<ServiceResult<PaymentResponse>> ConfirmPaymentAsync(
        Guid paymentId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return ServiceResult<PaymentResponse>.NotFound("Không tìm thấy khoản thanh toán.");

        if (!isAdmin && payment.Invoice.Contract.LandlordId != userId)
            return ServiceResult<PaymentResponse>.Forbidden();

        if (!string.Equals(payment.PaymentProvider, "Manual", StringComparison.Ordinal))
            return ServiceResult<PaymentResponse>.Conflict(
                "Chỉ khoản thanh toán thủ công mới được xác nhận tại endpoint này.");

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status == InvoiceStatus.Cancelled)
            return ServiceResult<PaymentResponse>.Conflict(
                "Khoản thanh toán không còn ở trạng thái chờ xác nhận.");

        var paidBefore = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != paymentId &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (paidBefore + payment.Amount > payment.Invoice.TotalAmount)
            return ServiceResult<PaymentResponse>.Conflict(
                "Xác nhận khoản này sẽ làm tổng thanh toán vượt hóa đơn.");

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = now;
        payment.ReviewedById = userId;
        payment.ReviewedAt = now;
        payment.FailureReason = null;

        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, paidBefore + payment.Amount);

        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.TenantId,
            "Thanh toán đã được xác nhận",
            $"Khoản thanh toán cho hóa đơn {payment.Invoice.InvoiceCode} đã được xác nhận.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(ToResponse(payment));
    }

    public async Task<ServiceResult<PaymentResponse>> RejectPaymentAsync(
        Guid paymentId, Guid userId, bool isAdmin, RejectPaymentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return ServiceResult<PaymentResponse>.NotFound("Không tìm thấy khoản thanh toán.");

        if (!isAdmin && payment.Invoice.Contract.LandlordId != userId)
            return ServiceResult<PaymentResponse>.Forbidden();

        if (!string.Equals(payment.PaymentProvider, "Manual", StringComparison.Ordinal))
            return ServiceResult<PaymentResponse>.Conflict(
                "Chỉ khoản thanh toán thủ công mới được từ chối tại endpoint này.");

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status == InvoiceStatus.Cancelled)
            return ServiceResult<PaymentResponse>.Conflict(
                "Chỉ khoản thanh toán đang chờ trên hóa đơn chưa hủy mới được từ chối.");

        var reason = request.Reason.Trim();
        if (reason.Length == 0)
            return ServiceResult<PaymentResponse>.BadRequest("Lý do từ chối không được để trống.");

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;
        payment.ReviewedById = userId;
        payment.ReviewedAt = now;

        var totalPaid = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != paymentId &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, totalPaid);

        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.TenantId,
            "Thanh toán bị từ chối",
            $"Khoản thanh toán cho hóa đơn {payment.Invoice.InvoiceCode} bị từ chối: {reason}",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(ToResponse(payment));
    }

    public async Task EnsureScheduledInvoicesAsync(
        Guid userId, bool isLandlord, bool isTenant, CancellationToken cancellationToken)
    {
        if (!isLandlord && !isTenant) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var contracts = await db.RentalContracts
            .Where(x => x.Status == RentalContractStatus.Active &&
                ((isLandlord && x.LandlordId == userId) || (isTenant && x.TenantId == userId)))
            .ToListAsync(cancellationToken);

        foreach (var contract in contracts)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var existing = await db.Invoices
                .Where(x => x.ContractId == contract.Id && x.Status != InvoiceStatus.Cancelled)
                .Select(x => new { x.InvoiceType, x.PeriodStart, x.PeriodEnd })
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var missing = new List<Invoice>();
            var periodIndex = 0;
            var periodStart = contract.StartDate;

            while (periodStart < contract.EndDate)
            {
                var nominalEnd = contract.StartDate.AddMonths(periodIndex + 1);
                var periodEnd = nominalEnd < contract.EndDate ? nominalEnd : contract.EndDate;
                var alreadyExists = existing.Any(x =>
                    x.InvoiceType == InvoiceType.MonthlyRent &&
                    x.PeriodStart == periodStart &&
                    x.PeriodEnd == periodEnd);

                // Không tự tạo công nợ cho các kỳ đã kết thúc trước khi tính năng được triển khai.
                // Chỉ bù kỳ hiện tại còn hiệu lực và các kỳ tương lai của hợp đồng đang hoạt động.
                if (!alreadyExists && periodEnd > today)
                {
                    var isShortFirstAndOnlyPeriod = periodIndex == 0 && periodEnd < nominalEnd;
                    var amount = isShortFirstAndOnlyPeriod
                        ? contract.MonthlyRent
                        : periodEnd == nominalEnd
                            ? contract.MonthlyRent
                            : decimal.Round(
                                contract.MonthlyRent * (periodEnd.DayNumber - periodStart.DayNumber) / 30m,
                                0,
                                MidpointRounding.AwayFromZero);

                    var dueDay = Math.Min(contract.PaymentDueDay,
                        DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
                    var configuredDueDate = new DateOnly(periodStart.Year, periodStart.Month, dueDay);
                    var dueDate = configuredDueDate < periodStart ? periodStart : configuredDueDate;
                    if (dueDate < today) dueDate = today;

                    missing.Add(new Invoice
                    {
                        ContractId = contract.Id,
                        InvoiceCode = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                        InvoiceType = InvoiceType.MonthlyRent,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        IssueDate = today,
                        DueDate = dueDate,
                        TotalAmount = amount,
                        Status = InvoiceStatus.Issued,
                        CreatedAt = now,
                        Items = new List<InvoiceItem>
                        {
                            new InvoiceItem
                            {
                                Description = $"Tiền thuê kỳ {periodIndex + 1}",
                                Quantity = 1,
                                UnitPrice = amount,
                                Amount = amount
                            }
                        }
                    });
                }

                periodStart = periodEnd;
                periodIndex++;
            }

            if (missing.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            db.Invoices.AddRange(missing);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var invoice in missing.Where(x => x.DueDate <= today.AddDays(7)))
            {
                await notifications.CreateAsync(
                    contract.TenantId,
                    "Hóa đơn tiền thuê sắp đến hạn",
                    $"Hóa đơn {invoice.InvoiceCode} đã được bổ sung vào lịch thanh toán. Hạn thanh toán: {invoice.DueDate:dd/MM/yyyy}.",
                    NotificationType.Invoice,
                    invoice.Id,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task MarkOverdueInvoicesAsync(
        Guid userId, bool isLandlord, bool isTenant, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        await db.Invoices
            .Where(x =>
                ((isLandlord && x.Contract.LandlordId == userId) ||
                 (isTenant && x.Contract.TenantId == userId)) &&
                x.DueDate < today &&
                (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, InvoiceStatus.Overdue),
                cancellationToken);
    }

    public async Task RefreshOverdueStatusAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid &&
            invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
        {
            invoice.Status = InvoiceStatus.Overdue;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static InvoiceStatus CalculateInvoiceStatus(Invoice invoice, decimal totalPaid)
    {
        if (totalPaid >= invoice.TotalAmount)
            return InvoiceStatus.Paid;

        if (invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            return InvoiceStatus.Overdue;

        return totalPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued;
    }

    public static PaymentResponse ToResponse(Payment payment) =>
        new(
            payment.Id,
            payment.InvoiceId,
            payment.PayerId,
            payment.Amount,
            payment.PaymentMethod.ToString(),
            payment.Status.ToString(),
            payment.PaymentProvider,
            payment.TransactionCode,
            payment.PaidAt,
            payment.FailureReason,
            payment.ReviewedById,
            payment.ReviewedAt,
            payment.CreatedAt);

    private IQueryable<Invoice> GetInvoiceDetailQuery() =>
        db.Invoices
            .Include(x => x.Contract)
                .ThenInclude(x => x.Property)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsSplitQuery();

    private static InvoiceDetailResponse ToDetail(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.ContractId,
            invoice.Contract.ContractCode,
            invoice.Contract.PropertyId,
            invoice.Contract.Property.Title,
            invoice.Contract.LandlordId,
            invoice.Contract.TenantId,
            invoice.InvoiceCode,
            invoice.InvoiceType.ToString(),
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.Status.ToString(),
            invoice.CreatedAt,
            invoice.Items.Select(x => new InvoiceItemResponse(
                x.Id, x.Description, x.Quantity, x.UnitPrice, x.Amount)).ToList(),
            invoice.Payments.OrderBy(x => x.CreatedAt).Select(ToResponse).ToList());
}
