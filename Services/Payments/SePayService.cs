using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Services.Payments;

public sealed class SePayService(
    AppDbContext db,
    IConfiguration configuration,
    INotificationService notifications,
    ILogger<SePayService> logger) : ISePayService
{
    private const string CheckoutUrl = "https://pay.sepay.vn/v1/checkout/init";
    private static readonly TimeSpan CallbackTolerance = TimeSpan.FromMinutes(5);
    private static readonly Regex PaymentCodePattern = new(
        @"^TN\d{8}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public async Task<SePayCheckoutResponse> CreateCheckoutAsync(
        Guid invoiceId,
        Guid tenantId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount || decimal.Truncate(amount) != amount)
            throw new ArgumentException("Số tiền thanh toán phải là số VND nguyên dương.", nameof(amount));

        var merchant = RequiredSetting("MerchantId");
        var secret = RequiredSetting("SecretKey");
        var publicUrl = RequiredHttpsUrl("PublicUrl");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var invoice = await db.Invoices
            .Include(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null || invoice.Contract.TenantId != tenantId)
            throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            throw new InvalidOperationException("Hóa đơn không ở trạng thái được phép thanh toán.");

        var existingCheckout = await db.Payments
            .SingleOrDefaultAsync(x =>
                x.InvoiceId == invoiceId &&
                x.PaymentProvider == "SePay" &&
                x.Status == PaymentStatus.Pending,
                cancellationToken);

        if (existingCheckout is not null && existingCheckout.Amount != amount)
            throw new InvalidOperationException("Hóa đơn đang có một phiên SePay chờ thanh toán với số tiền khác.");

        var reservedAmount = await db.Payments
            .Where(x => x.InvoiceId == invoiceId &&
                (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Succeeded))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (existingCheckout is null && reservedAmount + amount > invoice.TotalAmount)
            throw new InvalidOperationException("Số tiền thanh toán vượt quá số dư hóa đơn còn lại.");

        if (existingCheckout is not null && reservedAmount > invoice.TotalAmount)
            throw new InvalidOperationException("Các khoản thanh toán đang chờ đã vượt quá tổng hóa đơn.");

        var payment = existingCheckout;
        if (payment is null)
        {
            string paymentCode = string.Empty;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                paymentCode = $"TN{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}";
                if (!await db.Payments.AnyAsync(x => x.TransactionCode == paymentCode, cancellationToken))
                    break;

                paymentCode = string.Empty;
            }

            if (paymentCode.Length == 0)
                throw new InvalidOperationException("Không thể tạo mã thanh toán duy nhất. Vui lòng thử lại.");

            payment = new Payment
            {
                InvoiceId = invoiceId,
                PayerId = tenantId,
                Amount = amount,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaymentProvider = "SePay",
                TransactionCode = paymentCode,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.GetBaseException() is SqlException sqlException &&
                sqlException.Number is 2601 or 2627)
            {
                throw new InvalidOperationException("Mã thanh toán vừa được sử dụng. Vui lòng thử lại.", exception);
            }
        }

        var invoiceNumber = payment.TransactionCode!;
        var description = $"Thanh toan hoa don {invoice.InvoiceCode}";
        if (description.Length > 255)
            description = description[..255];

        var fields = new Dictionary<string, string>
        {
            ["order_amount"] = amount.ToString("0", CultureInfo.InvariantCulture),
            ["merchant"] = merchant,
            ["currency"] = "VND",
            ["operation"] = "PURCHASE",
            ["order_description"] = description,
            ["order_invoice_number"] = invoiceNumber,
            ["customer_id"] = tenantId.ToString("N"),
            ["payment_method"] = "BANK_TRANSFER",
            ["success_url"] = $"{publicUrl}/api/sepay/return/success",
            ["error_url"] = $"{publicUrl}/api/sepay/return/error",
            ["cancel_url"] = $"{publicUrl}/api/sepay/return/cancel"
        };

        fields["signature"] = CreateGatewaySignature(fields, secret);

        await transaction.CommitAsync(cancellationToken);

        return new SePayCheckoutResponse(
            payment.Id,
            invoiceNumber,
            CheckoutUrl,
            fields);
    }

    public async Task<bool> HandleGatewayIpnAsync(
        string rawBody,
        string? secretHeader,
        CancellationToken cancellationToken)
    {
        var expectedSecret = RequiredSetting("IpnSecret");
        if (!FixedTimeEquals(expectedSecret, secretHeader))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (!TryReadInt64(root, "timestamp", out var timestamp) ||
            !IsTimestampValid(timestamp) ||
            !TryReadString(root, "notification_type", out var notificationType) ||
            !root.TryGetProperty("order", out var order) ||
            !TryReadString(order, "order_invoice_number", out var invoiceNumber) ||
            !TryReadString(order, "order_currency", out var currency) ||
            !TryReadDecimal(order, "order_amount", out var orderAmount) ||
            currency != "VND")
            return false;

        if (!PaymentCodePattern.IsMatch(invoiceNumber))
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x =>
                x.TransactionCode == invoiceNumber &&
                x.PaymentProvider != null &&
                x.PaymentProvider.StartsWith("SePay"),
                cancellationToken);

        if (payment is null || payment.Amount != orderAmount)
            return false;

        if (notificationType == "TRANSACTION_VOID")
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Cancelled;
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (notificationType != "ORDER_PAID" ||
            !TryReadString(order, "order_status", out var orderStatus) ||
            orderStatus != "CAPTURED" ||
            !root.TryGetProperty("transaction", out var providerTransaction) ||
            !TryReadString(providerTransaction, "transaction_id", out var transactionId) ||
            !TryReadString(providerTransaction, "transaction_status", out var transactionStatus) ||
            transactionStatus != "APPROVED" ||
            !TryReadString(providerTransaction, "transaction_currency", out var transactionCurrency) ||
            transactionCurrency != "VND" ||
            !TryReadDecimal(providerTransaction, "transaction_amount", out var transactionAmount) ||
            transactionAmount != payment.Amount)
            return false;

        var providerReference = $"SePay:Gateway:{transactionId}";
        if (payment.Status == PaymentStatus.Succeeded)
        {
            // Có thể Landlord/Admin đã xác nhận thủ công trước khi IPN tới.
            // Không ghi nhận lần hai, nhưng ACK để SePay không retry vô hạn.
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            return false;

        if (await db.Payments.AnyAsync(x =>
                x.Id != payment.Id && x.PaymentProvider == providerReference,
                cancellationToken))
            return false;

        var paidBefore = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != payment.Id &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (paidBefore + payment.Amount > payment.Invoice.TotalAmount)
            return false;

        payment.Status = PaymentStatus.Succeeded;
        payment.PaymentProvider = providerReference;
        payment.PaymentMethod = MapPaymentMethod(
            TryReadString(providerTransaction, "payment_method", out var method) ? method : null);
        payment.PaidAt = DateTime.UtcNow;
        payment.FailureReason = null;
        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, paidBefore + payment.Amount);

        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.LandlordId,
            "Thanh toán qua SePay thành công",
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
        var webhookSecret = RequiredSetting("WebhookSecret");
        var expectedAccount = RequiredSetting("BankAccountNumber");

        if (!VerifyWebhookSignature(rawBody, signature, timestamp, webhookSecret))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (!TryReadInt64(root, "id", out var eventId) ||
            !TryReadString(root, "transferType", out var transferType) ||
            !TryReadString(root, "accountNumber", out var accountNumber) ||
            !TryReadDecimal(root, "transferAmount", out var transferAmount) ||
            transferAmount <= 0)
            return false;

        if (!FixedTimeEquals(expectedAccount, accountNumber))
            return false;

        // Giao dịch tiền ra không tác động đến hóa đơn nhưng vẫn được xác thực và ACK.
        if (!string.Equals(transferType, "in", StringComparison.OrdinalIgnoreCase))
            return true;

        var code = TryReadString(root, "code", out var suppliedCode) && !string.IsNullOrWhiteSpace(suppliedCode)
            ? suppliedCode.Trim().ToUpperInvariant()
            : ExtractPaymentCode(TryReadString(root, "content", out var content) ? content : string.Empty);

        if (code is null || !PaymentCodePattern.IsMatch(code))
        {
            logger.LogInformation("SePay webhook {EventId} không có mã thanh toán của hệ thống.", eventId);
            return true;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x =>
                x.TransactionCode == code &&
                x.PaymentProvider != null &&
                x.PaymentProvider.StartsWith("SePay"),
                cancellationToken);

        if (payment is null)
        {
            logger.LogInformation("SePay webhook {EventId} không khớp khoản thanh toán nào.", eventId);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid ||
            payment.Amount != transferAmount)
        {
            logger.LogWarning(
                "SePay webhook {EventId} không khớp trạng thái hoặc số tiền của payment {PaymentId}.",
                eventId,
                payment.Id);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var providerReference = $"SePay:Bank:{eventId}";
        if (await db.Payments.AnyAsync(x =>
                x.Id != payment.Id && x.PaymentProvider == providerReference,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var paidBefore = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != payment.Id &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (paidBefore + payment.Amount > payment.Invoice.TotalAmount)
        {
            logger.LogWarning("SePay webhook {EventId} sẽ làm vượt tổng hóa đơn {InvoiceId}.", eventId, payment.InvoiceId);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.PaymentProvider = providerReference;
        payment.PaymentMethod = PaymentMethod.BankTransfer;
        payment.PaidAt = DateTime.UtcNow;
        payment.FailureReason = null;
        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, paidBefore + payment.Amount);

        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            payment.Invoice.Contract.LandlordId,
            "Đã nhận chuyển khoản qua SePay",
            $"Khoản thanh toán cho hóa đơn {payment.Invoice.InvoiceCode} đã được xác nhận qua ngân hàng.",
            NotificationType.Payment,
            payment.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string CreateGatewaySignature(
        IReadOnlyDictionary<string, string> fields,
        string secret)
    {
        var signedString = string.Join(",",
            SignedFields
                .Where(fields.ContainsKey)
                .Select(key => $"{key}={fields[key]}"));

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedString));

        return Convert.ToBase64String(hash);
    }

    private static bool VerifyWebhookSignature(
        string rawBody,
        string? signature,
        string? timestamp,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTime) ||
            !IsTimestampValid(unixTime))
            return false;

        var signedContent = $"{timestamp}.{rawBody}";
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedContent));
        var expected = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

        return FixedTimeEquals(expected, signature);
    }

    private static bool IsTimestampValid(long timestamp)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return timestamp >= now - (long)CallbackTolerance.TotalSeconds &&
               timestamp <= now + (long)CallbackTolerance.TotalSeconds;
    }

    private string RequiredSetting(string name) =>
        configuration[$"SePay:{name}"] ??
        throw new InvalidOperationException($"Thiếu cấu hình SePay:{name}.");

    private string RequiredHttpsUrl(string name)
    {
        var value = RequiredSetting(name).TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.IsLoopback ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SePay:{name} phải là URL HTTPS công khai hợp lệ.");

        return value;
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;

        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length &&
               CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static bool TryReadString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var item) ||
            item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;

        value = item.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadDecimal(JsonElement element, string property, out decimal value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var item) &&
               decimal.TryParse(item.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt64(JsonElement element, string property, out long value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var item) &&
               long.TryParse(item.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string? ExtractPaymentCode(string content)
    {
        var match = PaymentCodePattern.Match(content);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static PaymentMethod MapPaymentMethod(string? method) =>
        method?.ToUpperInvariant() switch
        {
            "BANK_TRANSFER" or "NAPAS_BANK_TRANSFER" => PaymentMethod.BankTransfer,
            _ => PaymentMethod.Other
        };

    private static InvoiceStatus CalculateInvoiceStatus(Invoice invoice, decimal totalPaid)
    {
        if (totalPaid >= invoice.TotalAmount)
            return InvoiceStatus.Paid;

        if (invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return InvoiceStatus.Overdue;

        return totalPaid > 0
            ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Issued;
    }
}
