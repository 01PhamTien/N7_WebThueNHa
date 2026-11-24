using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;

namespace Web2_ThueNha.Services;

public sealed class SePayService(
    PaymentDbContext db,
    IConfiguration configuration,
    ILogger<SePayService> logger) : ISePayService
{
    private static readonly string[] SignedFields =
    [
        "order_amount", "merchant", "currency", "operation", "order_description",
        "order_invoice_number", "customer_id", "payment_method",
        "success_url", "error_url", "cancel_url"
    ];

    public async Task<SePayCheckout> CreateCheckoutAsync(
        CreateSePayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "Số tiền phải lớn hơn 0 và tối đa 2 chữ số thập phân.");

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 255)
            throw new ArgumentException("Mô tả thanh toán không hợp lệ.", nameof(request.Description));

        var merchant = RequiredSetting("SEPAY_MERCHANT_ID_PRODUCTION");
        var secret = RequiredSetting("SEPAY_SECRET_KEY_PRODUCTION");
        var publicUrl = RequiredSetting("SEPAY_PUBLIC_URL").TrimEnd('/');
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("SEPAY_PUBLIC_URL phải là URL HTTPS công khai.");

        var invoice = $"TN{DateTime.UtcNow:yyMMddHHmmss}{RandomNumberGenerator.GetInt32(1000, 9999)}";
        var fields = new Dictionary<string, string>
        {
            ["order_amount"] = request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["merchant"] = merchant,
            ["currency"] = "VND",
            ["operation"] = "PURCHASE",
            ["order_description"] = request.Description.Trim(),
            ["order_invoice_number"] = invoice,
            ["success_url"] = $"{publicUrl}/api/sepay/return/success",
            ["error_url"] = $"{publicUrl}/api/sepay/return/error",
            ["cancel_url"] = $"{publicUrl}/api/sepay/return/cancel"
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            fields["customer_id"] = request.CustomerId.Trim();

        fields["signature"] = CreateGatewaySignature(fields, secret);

        db.Payments.Add(new PaymentRecord
        {
            InvoiceNumber = invoice,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            CustomerId = request.CustomerId?.Trim(),
            Status = "Pending"
        });
        await db.SaveChangesAsync(cancellationToken);

        return new SePayCheckout(
            invoice,
            "https://pay.sepay.vn/v1/checkout/init",
            fields);
    }

    public async Task<bool> HandleGatewayIpnAsync(
        string rawBody,
        string? secretHeader,
        CancellationToken cancellationToken)
    {
        var expectedSecret = RequiredSetting("SEPAY_IPN_SECRET_PRODUCTION");
        if (!FixedTimeEquals(expectedSecret, secretHeader))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var timestamp = root.GetProperty("timestamp").GetInt64();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (timestamp < now - 300 || timestamp > now + 300)
            return false;

        var notificationType = root.GetProperty("notification_type").GetString();
        var order = root.GetProperty("order");
        var invoice = order.GetProperty("order_invoice_number").GetString();
        var amountText = order.GetProperty("order_amount").GetString();

        if (string.IsNullOrWhiteSpace(invoice) ||
            !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return false;

        var payment = await db.Payments.SingleOrDefaultAsync(
            x => x.InvoiceNumber == invoice, cancellationToken);
        if (payment is null || payment.Amount != amount)
            return false;

        if (notificationType == "ORDER_PAID")
        {
            var status = order.GetProperty("order_status").GetString();
            if (status != "CAPTURED")
                return false;

            var transaction = root.GetProperty("transaction");
            var transactionId = transaction.GetProperty("transaction_id").GetString();
            if (string.IsNullOrWhiteSpace(transactionId))
                return false;

            if (payment.Status == "Paid")
                return payment.ProviderTransactionId == transactionId;

            payment.Status = "Paid";
            payment.ProviderTransactionId = transactionId;
            payment.PaymentMethod = transaction.TryGetProperty("payment_method", out var method)
                ? method.GetString() : null;
            payment.PaidAt = DateTimeOffset.UtcNow;
        }
        else if (notificationType == "TRANSACTION_VOID")
        {
            if (payment.Status != "Paid")
                payment.Status = "Cancelled";
        }
        else
        {
            return false;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleBankWebhookAsync(
        string rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken)
    {
        var secret = RequiredSetting("SEPAY_WEBHOOK_SECRET_PRODUCTION");
        if (!VerifyWebhookSignature(rawBody, signature, timestamp, secret))
            return false;

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var eventId = root.TryGetProperty("id", out var idElement)
            ? idElement.ToString()
            : null;
        var transferType = ReadString(root, "transferType");
        var transferAmount = ReadDecimal(root, "transferAmount");
        var content = ReadString(root, "content") ?? ReadString(root, "description") ?? string.Empty;
        var reference = ReadString(root, "referenceCode");
        var expectedAccount = RequiredSetting("SEPAY_BANK_ACCOUNT_NUMBER_PRODUCTION");
        var actualAccount = ReadString(root, "accountNumber");
        if (string.IsNullOrWhiteSpace(actualAccount) ||
            !FixedTimeEquals(expectedAccount, actualAccount))
            return false;

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        var eventKey = !string.IsNullOrWhiteSpace(eventId)
            ? $"bank:{eventId}"
            : $"bank:sha256:{payloadHash}";

        if (string.Equals(transferType, "out", StringComparison.OrdinalIgnoreCase) ||
            transferAmount is null || transferAmount <= 0)
            return true;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.WebhookEvents.AnyAsync(x => x.EventKey == eventKey, cancellationToken))
            return true;

        db.WebhookEvents.Add(new SePayWebhookEvent
        {
            EventKey = eventKey,
            Source = "BankWebhook",
            RawPayload = rawBody
        });

        var invoice = ExtractInvoice(content);
        if (!string.IsNullOrWhiteSpace(invoice))
        {
            var payment = await db.Payments.SingleOrDefaultAsync(
                x => x.InvoiceNumber == invoice, cancellationToken);

            if (payment is not null &&
                payment.Status == "Pending" &&
                payment.Amount == transferAmount.Value)
            {
                payment.Status = "Paid";
                payment.ProviderTransactionId = reference ?? eventId;
                payment.PaymentMethod = "BANK_TRANSFER";
                payment.PaidAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string CreateGatewaySignature(
        IReadOnlyDictionary<string, string> fields,
        string secret)
    {
        var signedString = string.Join(",",
            SignedFields.Where(fields.ContainsKey)
                .Select(key => $"{key}={fields[key]}"));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedString));
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyWebhookSignature(
        string rawBody,
        string? signature,
        string? timestamp,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTime))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (unixTime < now - 300 || unixTime > now + 300)
            return false;

        var signedContent = $"{timestamp}.{rawBody}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedContent));
        var expected = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
        return FixedTimeEquals(expected, signature);
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;

        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private string RequiredSetting(string key) =>
        configuration[key] ?? Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Thiếu cấu hình bí mật {key}.");

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.ToString() : null;

    private static decimal? ReadDecimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount : null;

    private static string? ExtractInvoice(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content, @"TN\d{14}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }
}
