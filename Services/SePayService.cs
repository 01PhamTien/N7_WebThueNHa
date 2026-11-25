using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Services;

public sealed class SePayService(
    AppDbContext db,
    IConfiguration configuration,
    INotificationService notifications,
    ILogger<SePayService> logger) : ISePayService
{
    private const string PaymentCodePrefix = "SP";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private static readonly string[] SignedFields =
    [
        "order_amount",
        "merchant",
        "currency",
        "operation",
        "order_description",
        "order_invoice_number",
        "customer_id",
        "payment_method",
        "success_url",
        "error_url",
        "cancel_url"
    ];

    public async Task<SePayCheckout> CreateCheckoutAsync(
        Guid invoiceId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = GetCheckoutSettings();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var invoice = await db.Invoices
            .Include(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null || invoice.Contract.TenantId != tenantId)
            throw new SePayRequestException(StatusCodes.Status404NotFound, "Không tìm thấy hóa đơn.");

        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            throw new SePayRequestException(StatusCodes.Status409Conflict, "Hóa đơn không ở trạng thái được phép thanh toán.");

        var existingSePayPayment = await db.Payments
            .Include(x => x.Invoice)
            .Where(x => x.InvoiceId == invoiceId &&
                        x.PayerId == tenantId &&
                        x.PaymentProvider == "SePay" &&
                        x.Status == PaymentStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSePayPayment is not null)
        {
            var existingCheckout = BuildCheckout(existingSePayPayment, invoice.InvoiceCode, settings);
            await transaction.CommitAsync(cancellationToken);
            return existingCheckout;
        }

        var hasOtherPendingPayment = await db.Payments.AnyAsync(x =>
            x.InvoiceId == invoiceId && x.Status == PaymentStatus.Pending,
            cancellationToken);

        if (hasOtherPendingPayment)
            throw new SePayRequestException(
                StatusCodes.Status409Conflict,
                "Hóa đơn đang có yêu cầu thanh toán khác chờ xác nhận. Vui lòng xử lý yêu cầu đó trước.");

        var paidAmount = await db.Payments
            .Where(x => x.InvoiceId == invoiceId && x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var remainingAmount = invoice.TotalAmount - paidAmount;
        if (remainingAmount <= 0)
            throw new SePayRequestException(StatusCodes.Status409Conflict, "Hóa đơn đã được thanh toán đủ.");

        // Cổng thanh toán SePay chỉ hỗ trợ VND; không gửi số tiền có phần thập phân.
        if (remainingAmount != decimal.Truncate(remainingAmount))
            throw new SePayRequestException(
                StatusCodes.Status400BadRequest,
                "Số tiền VND phải là số nguyên trước khi tạo checkout SePay.");

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            PayerId = tenantId,
            Amount = remainingAmount,
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentProvider = "SePay",
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            invoice.Contract.LandlordId,
            "Có yêu cầu thanh toán qua SePay",
            $"Người thuê đã khởi tạo thanh toán SePay cho hóa đơn {invoice.InvoiceCode}.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return BuildCheckout(payment, invoice.InvoiceCode, settings);
    }

    public async Task<bool> HandleGatewayIpnAsync(
        string rawBody,
        string? secretHeader,
        CancellationToken cancellationToken)
    {
        if (!FixedTimeEquals(RequiredSetting("SEPAY_IPN_SECRET"), secretHeader))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (!root.TryGetProperty("timestamp", out var timestampElement) ||
            !timestampElement.TryGetInt64(out var timestamp))
            return false;

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // IPN có thể được SePay gửi lại sau thời gian retry; không loại IPN cũ hợp lệ.
        // Trạng thái Payment + TransactionCode unique đảm bảo callback cũ không ghi nhận hai lần.
        if (timestamp <= 0 || timestamp > nowSeconds + 300)
            return false;

        var notificationType = ReadString(root, "notification_type");
        if (string.IsNullOrWhiteSpace(notificationType) ||
            !root.TryGetProperty("order", out var order))
            return false;

        var orderCode = ReadString(order, "order_invoice_number");
        if (!TryDecodePaymentCode(orderCode, out var paymentId))
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null || payment.PaymentProvider != "SePay")
            return false;

        var orderAmount = ReadDecimal(order, "order_amount");
        var orderCurrency = ReadString(order, "order_currency");
        if (orderAmount != payment.Amount || orderCurrency != "VND")
            return false;

        if (notificationType == "TRANSACTION_VOID")
        {
            if (payment.Status == PaymentStatus.Cancelled)
                return true;
            if (payment.Status != PaymentStatus.Pending)
                return false;

            payment.Status = PaymentStatus.Cancelled;
            await RecalculateInvoiceStatusAsync(payment.Invoice, payment.Id, 0m, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (notificationType != "ORDER_PAID")
            return false;

        var orderStatus = ReadString(order, "order_status");
        if (orderStatus != "CAPTURED" ||
            !root.TryGetProperty("transaction", out var transactionElement))
            return false;

        var transactionStatus = ReadString(transactionElement, "transaction_status");
        var transactionCurrency = ReadString(transactionElement, "transaction_currency");
        var transactionAmount = ReadDecimal(transactionElement, "transaction_amount");
        var transactionId = ReadString(transactionElement, "transaction_id");

        if (transactionStatus != "APPROVED" ||
            transactionCurrency != "VND" ||
            transactionAmount != payment.Amount ||
            string.IsNullOrWhiteSpace(transactionId) ||
            transactionId.Length > 150)
            return false;

        // IPN có thể được gửi lại nhiều lần; payment đã thành công thì chỉ ACK, không phát notification lần nữa.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status == InvoiceStatus.Cancelled)
            return false;

        if (payment.Invoice.Status == InvoiceStatus.Paid)
        {
            logger.LogWarning("Bỏ qua IPN SePay vì hóa đơn {InvoiceId} đã Paid trước callback.", payment.InvoiceId);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var transactionAlreadyUsed = await db.Payments.AnyAsync(x =>
            x.Id != payment.Id && x.TransactionCode == transactionId,
            cancellationToken);
        if (transactionAlreadyUsed)
            return false;

        payment.Status = PaymentStatus.Succeeded;
        payment.PaymentMethod = PaymentMethod.BankTransfer;
        payment.TransactionCode = transactionId;
        payment.PaidAt = DateTime.UtcNow;

        await RecalculateInvoiceStatusAsync(payment.Invoice, payment.Id, payment.Amount, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            payment.PayerId,
            "Thanh toán SePay thành công",
            $"SePay đã xác nhận thanh toán cho hóa đơn {payment.Invoice.InvoiceCode}.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.LandlordId,
            "Đã nhận thanh toán qua SePay",
            $"Khoản thanh toán cho hóa đơn {payment.Invoice.InvoiceCode} đã được SePay xác nhận.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleBankWebhookAsync(
        string rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken)
    {
        var secret = RequiredSetting("SEPAY_WEBHOOK_SECRET");
        if (!VerifyBankWebhookSignature(rawBody, signature, timestamp, secret))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        var expectedAccount = RequiredSetting("SEPAY_BANK_ACCOUNT_NUMBER").Trim();
        var actualAccount = ReadString(root, "accountNumber")?.Trim();
        if (string.IsNullOrWhiteSpace(actualAccount) ||
            !FixedTimeEquals(expectedAccount, actualAccount))
            return false;

        var transferType = ReadString(root, "transferType");
        if (!string.Equals(transferType, "in", StringComparison.OrdinalIgnoreCase))
            return true;

        var transferAmount = ReadDecimal(root, "transferAmount");
        if (transferAmount is null || transferAmount <= 0)
            return true;

        var code = ReadString(root, "code");
        if (!TryDecodePaymentCode(code, out var paymentId))
        {
            var content = ReadString(root, "content") ?? ReadString(root, "description") ?? string.Empty;
            var match = Regex.Match(content, @"SP[A-Z2-7]{26}", RegexOptions.IgnoreCase);
            if (!match.Success || !TryDecodePaymentCode(match.Value, out paymentId))
                return true; // Giao dịch không có mã hóa đơn của hệ thống này.
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null || payment.PaymentProvider != "SePay")
        {
            logger.LogWarning("SePay bank webhook có mã thanh toán không tồn tại: {PaymentCode}", code);
            return true;
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status == InvoiceStatus.Cancelled ||
            payment.Amount != transferAmount.Value)
        {
            logger.LogWarning("Bỏ qua giao dịch SePay không khớp payment {PaymentId}", payment.Id);
            return true;
        }

        var eventId = ReadString(root, "id");
        var referenceCode = ReadString(root, "referenceCode");
        var eventKey = eventId ??
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)))[..24];
        var transactionCode = $"SEPAY-BANK-{eventKey}";
        logger.LogInformation(
            "Đối soát SePay bank event {EventId}, reference {ReferenceCode}, payment {PaymentId}",
            eventId, referenceCode, payment.Id);

        if (transactionCode.Length > 150)
            transactionCode = transactionCode[..150];

        var transactionAlreadyUsed = await db.Payments.AnyAsync(x =>
            x.Id != payment.Id && x.TransactionCode == transactionCode,
            cancellationToken);
        if (transactionAlreadyUsed)
        {
            logger.LogWarning("SePay bank transaction code đã được sử dụng: {TransactionCode}", transactionCode);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.PaymentMethod = PaymentMethod.BankTransfer;
        payment.TransactionCode = transactionCode;
        payment.PaidAt = DateTime.UtcNow;

        await RecalculateInvoiceStatusAsync(payment.Invoice, payment.Id, payment.Amount, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            payment.PayerId,
            "Thanh toán chuyển khoản đã được xác nhận",
            $"SePay đã ghi nhận giao dịch ngân hàng cho hóa đơn {payment.Invoice.InvoiceCode}.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.LandlordId,
            "Đã nhận chuyển khoản",
            $"SePay đã ghi nhận khoản thanh toán cho hóa đơn {payment.Invoice.InvoiceCode}.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RecalculateInvoiceStatusAsync(
        Invoice invoice,
        Guid currentPaymentId,
        decimal currentPaymentAmount,
        CancellationToken cancellationToken)
    {
        var paidBefore = await db.Payments
            .Where(x => x.InvoiceId == invoice.Id &&
                        x.Id != currentPaymentId &&
                        x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var totalPaid = paidBefore + currentPaymentAmount;
        if (totalPaid >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            return;
        }

        if (invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            invoice.Status = InvoiceStatus.Overdue;
            return;
        }

        invoice.Status = totalPaid > 0
            ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Issued;
    }

    private SePayCheckout BuildCheckout(Payment payment, string invoiceCode, PaymentCodeSettings settings)
    {
        if (payment.Amount != decimal.Truncate(payment.Amount))
            throw new SePayRequestException(StatusCodes.Status400BadRequest, "SePay chỉ nhận số tiền VND nguyên.");

        var orderInvoiceNumber = EncodePaymentCode(payment.Id);
        var description = $"Thanh toan hoa don {invoiceCode}";
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_amount"] = payment.Amount.ToString("0", CultureInfo.InvariantCulture),
            ["merchant"] = settings.MerchantId,
            ["currency"] = "VND",
            ["operation"] = "PURCHASE",
            ["order_description"] = description,
            ["order_invoice_number"] = orderInvoiceNumber,
            ["customer_id"] = payment.PayerId.ToString("N"),
            ["payment_method"] = "BANK_TRANSFER",
            ["success_url"] = $"{settings.PublicUrl}/api/sepay/return/success",
            ["error_url"] = $"{settings.PublicUrl}/api/sepay/return/error",
            ["cancel_url"] = $"{settings.PublicUrl}/api/sepay/return/cancel"
        };

        var signedString = string.Join(",",
            SignedFields.Where(fields.ContainsKey).Select(key => $"{key}={fields[key]}"));
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(settings.SecretKey),
            Encoding.UTF8.GetBytes(signedString));
        var signature = Convert.ToBase64String(hash);

        var orderedFields = SignedFields
            .Where(fields.ContainsKey)
            .Select(key => new SePayFormField(key, fields[key]))
            .ToList();
        orderedFields.Add(new SePayFormField("signature", signature));

        return new SePayCheckout(payment.Id, orderInvoiceNumber, settings.CheckoutUrl, orderedFields);
    }

    private PaymentCodeSettings GetCheckoutSettings()
    {
        var merchantId = RequiredSetting("SEPAY_MERCHANT_ID");
        var secretKey = RequiredSetting("SEPAY_SECRET_KEY");
        var checkoutUrl = RequiredSetting("SEPAY_CHECKOUT_INIT_URL");
        var publicUrl = RequiredSetting("SEPAY_PUBLIC_URL").TrimEnd('/');

        if (!Uri.TryCreate(checkoutUrl, UriKind.Absolute, out var checkoutUri) ||
            checkoutUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("SEPAY_CHECKOUT_INIT_URL phải là URL HTTPS của SePay.");

        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri) ||
            publicUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(publicUri.UserInfo) ||
            publicUri.IsLoopback ||
            publicUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IsNonPublicIpAddress(publicUri.Host))
            throw new InvalidOperationException("SEPAY_PUBLIC_URL phải là domain/IP HTTPS công khai, không được là địa chỉ nội bộ.");

        return new PaymentCodeSettings(merchantId, secretKey, checkoutUri.ToString(), publicUri.GetLeftPart(UriPartial.Authority));
    }

    private string RequiredSetting(string key) =>
        configuration[key] ??
        throw new InvalidOperationException($"Thiếu cấu hình {key}.");

    private static bool IsNonPublicIpAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
            return false;

        if (IPAddress.IsLoopback(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal)
            return true;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xFE) == 0xFC;

        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;
        return bytes[0] is 0 or 10 or 127 ||
               (bytes[0] == 169 && bytes[1] == 254) ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private static bool VerifyBankWebhookSignature(
        string rawBody,
        string? signature,
        string? timestamp,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTimestamp))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (unixTimestamp < now - 300 || unixTimestamp > now + 300)
            return false;

        var signedPayload = $"{timestamp}.{rawBody}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expected = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
        return FixedTimeEquals(expected, signature);
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;

    private static decimal? ReadDecimal(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value))
            return null;

        return decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    private static string EncodePaymentCode(Guid paymentId)
    {
        var bytes = paymentId.ToByteArray();
        var output = new StringBuilder(28).Append(PaymentCodePrefix);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);

        return output.ToString();
    }

    private static bool TryDecodePaymentCode(string? code, out Guid paymentId)
    {
        paymentId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().ToUpperInvariant();
        if (!normalized.StartsWith(PaymentCodePrefix, StringComparison.Ordinal) ||
            normalized.Length != 28)
            return false;

        var bytes = new byte[16];
        var buffer = 0;
        var bitsLeft = 0;
        var byteIndex = 0;

        foreach (var character in normalized[2..])
        {
            var value = Base32Alphabet.IndexOf(character);
            if (value < 0)
                return false;

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                if (byteIndex >= bytes.Length)
                    return false;
                bytes[byteIndex++] = (byte)((buffer >> bitsLeft) & 255);
            }
        }

        if (byteIndex != 16 || bitsLeft != 2 || (buffer & 3) != 0)
            return false;

        paymentId = new Guid(bytes);
        return true;
    }

    private sealed record PaymentCodeSettings(
        string MerchantId,
        string SecretKey,
        string CheckoutUrl,
        string PublicUrl);
}
