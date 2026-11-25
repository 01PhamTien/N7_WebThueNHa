using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Contracts;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Services.Contracts;

public sealed class ContractService(AppDbContext db, INotificationService notifications) : IContractService
{
    public async Task<ServiceResult<RentalContractResponse>> CreateFromBookingAsync(
        Guid bookingId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var booking = await db.Bookings
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
            return ServiceResult<RentalContractResponse>.NotFound("Không tìm thấy yêu cầu đặt thuê.");

        if (booking.TenantId != userId && booking.Property.LandlordId != userId)
            return ServiceResult<RentalContractResponse>.Forbidden();

        if (booking.Status != BookingStatus.Accepted)
            return ServiceResult<RentalContractResponse>.Conflict(
                "Chỉ yêu cầu đặt thuê đã được chấp nhận mới tạo được hợp đồng.");

        if (booking.EndDate <= booking.StartDate || booking.ProposedPrice <= 0)
            return ServiceResult<RentalContractResponse>.Conflict(
                "Thông tin ngày thuê hoặc giá thuê của yêu cầu không hợp lệ.");

        var existing = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (existing is not null)
            return ServiceResult<RentalContractResponse>.Success(ToResponse(existing));

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var contract = new RentalContract
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            LandlordId = booking.Property.LandlordId,
            TenantId = booking.TenantId,
            ContractCode = $"HD-{now:yyyyMMdd}-{suffix}",
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            MonthlyRent = booking.ProposedPrice,
            DepositAmount = 0,
            PaymentDueDay = 1,
            Status = RentalContractStatus.Draft,
            CreatedAt = now
        };

        db.RentalContracts.Add(contract);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.Entry(contract).State = EntityState.Detached;

            var concurrentContract = await db.RentalContracts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

            if (concurrentContract is not null)
                return ServiceResult<RentalContractResponse>.Success(ToResponse(concurrentContract));

            throw;
        }

        var recipientId = booking.TenantId == userId
            ? booking.Property.LandlordId
            : booking.TenantId;
        await notifications.CreateAsync(
            recipientId,
            "Hợp đồng thuê đã được tạo",
            $"Một hợp đồng nháp đã được tạo cho căn nhà {booking.Property.Title}.",
            NotificationType.Contract,
            contract.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<RentalContractResponse>.Created(ToResponse(contract));
    }

    public async Task<ServiceResult<RentalContractResponse>> UpdateAsync(
        Guid contractId, Guid userId, UpdateRentalContractRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return ServiceResult<RentalContractResponse>.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return ServiceResult<RentalContractResponse>.NotFound("Không tìm thấy hợp đồng.");

        if (contract.LandlordId != userId)
            return ServiceResult<RentalContractResponse>.Forbidden();

        if (contract.Status is not (RentalContractStatus.Draft or RentalContractStatus.AwaitingSignature))
            return ServiceResult<RentalContractResponse>.Conflict(
                "Không thể chỉnh sửa hợp đồng đã Active hoặc đã kết thúc.");

        if (contract.Status == RentalContractStatus.AwaitingSignature)
        {
            contract.LandlordSignedAt = null;
            contract.TenantSignedAt = null;
            contract.SignedAt = null;
            contract.Status = RentalContractStatus.Draft;
        }

        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.MonthlyRent = request.MonthlyRent;
        contract.DepositAmount = request.DepositAmount;
        contract.PaymentDueDay = request.PaymentDueDay;
        contract.Terms = string.IsNullOrWhiteSpace(request.Terms) ? null : request.Terms.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<RentalContractResponse>.Success(ToResponse(contract));
    }



    public async Task<ServiceResult<RentalContractResponse>> SubmitForSignatureAsync(
        Guid contractId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return ServiceResult<RentalContractResponse>.NotFound("Không tìm thấy hợp đồng.");

        if (contract.LandlordId != userId)
            return ServiceResult<RentalContractResponse>.Forbidden();

        if (contract.Status != RentalContractStatus.Draft)
            return ServiceResult<RentalContractResponse>.Conflict("Chỉ hợp đồng Draft mới được gửi xác nhận.");

        contract.Status = RentalContractStatus.AwaitingSignature;
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            contract.TenantId,
            "Hợp đồng đang chờ bạn xác nhận",
            $"Chủ nhà đã gửi hợp đồng {contract.ContractCode} để bạn xem và xác nhận.",
            NotificationType.Contract,
            contract.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<RentalContractResponse>.Success(ToResponse(contract));
    }

    public async Task<ServiceResult<RentalContractResponse>> SignAsync(
        Guid contractId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return ServiceResult<RentalContractResponse>.NotFound("Không tìm thấy hợp đồng.");

        var isLandlord = contract.LandlordId == userId;
        var isTenant = contract.TenantId == userId;
        if (!isLandlord && !isTenant)
            return ServiceResult<RentalContractResponse>.Forbidden();

        if (isLandlord && isTenant)
            return ServiceResult<RentalContractResponse>.Conflict(
                "Một tài khoản không thể xác nhận thay cả hai bên.");

        var alreadySignedByCurrentUser = isLandlord
            ? contract.LandlordSignedAt.HasValue
            : contract.TenantSignedAt.HasValue;

        if (contract.Status == RentalContractStatus.Active && alreadySignedByCurrentUser)
            return ServiceResult<RentalContractResponse>.Success(ToResponse(contract));

        if (contract.Status != RentalContractStatus.AwaitingSignature)
            return ServiceResult<RentalContractResponse>.Conflict(
                "Chỉ hợp đồng đang chờ xác nhận mới được ký.");

        if (!alreadySignedByCurrentUser)
        {
            var signedAt = DateTime.UtcNow;
            if (isLandlord)
                contract.LandlordSignedAt = signedAt;
            else
                contract.TenantSignedAt = signedAt;
        }

        if (contract.LandlordSignedAt.HasValue && contract.TenantSignedAt.HasValue)
        {
            contract.SignedAt = contract.LandlordSignedAt.Value >= contract.TenantSignedAt.Value
                ? contract.LandlordSignedAt.Value
                : contract.TenantSignedAt.Value;
            contract.Status = RentalContractStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (!alreadySignedByCurrentUser)
        {
            var recipientId = isLandlord ? contract.TenantId : contract.LandlordId;
            var content = contract.Status == RentalContractStatus.Active
                ? $"Hợp đồng {contract.ContractCode} đã được cả hai bên xác nhận và có hiệu lực."
                : $"Bên còn lại đã xác nhận hợp đồng {contract.ContractCode}. Vui lòng hoàn tất chữ ký của bạn.";
            await notifications.CreateAsync(
                recipientId,
                contract.Status == RentalContractStatus.Active
                    ? "Hợp đồng đã có hiệu lực"
                    : "Hợp đồng vừa được xác nhận",
                content,
                NotificationType.Contract,
                contract.Id,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<RentalContractResponse>.Success(ToResponse(contract));
    }

    private static RentalContractResponse ToResponse(RentalContract contract) =>
        new(
            contract.Id,
            contract.BookingId,
            contract.PropertyId,
            contract.LandlordId,
            contract.TenantId,
            contract.ContractCode,
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.PaymentDueDay,
            contract.Terms,
            contract.ContractUrl,
            contract.Status.ToString(),
            contract.LandlordSignedAt,
            contract.TenantSignedAt,
            contract.SignedAt,
            contract.CreatedAt);
}
