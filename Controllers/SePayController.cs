using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Services;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/sepay")]
public sealed class SePayController(
    ISePayService sePayService,
    PaymentDbContext db,
    IConfiguration configuration,
    ILogger<SePayController> logger) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateSePayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsInternalRequestAuthorized())
            return Unauthorized(new { message = "Thiếu hoặc sai khóa truy cập dịch vụ thanh toán." });

        try
        {
            var checkout = await sePayService.CreateCheckoutAsync(request, cancellationToken);
            return Ok(checkout);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Cấu hình SePay chưa đầy đủ.");
            return Problem("Cổng thanh toán chưa được cấu hình đầy đủ.", statusCode: 503);
        }
    }

    [HttpPost("ipn")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> GatewayIpn(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var accepted = await sePayService.HandleGatewayIpnAsync(
                rawBody, Request.Headers["X-Secret-Key"].FirstOrDefault(), cancellationToken);
            return accepted ? Ok(new { success = true }) : Unauthorized(new { success = false });
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or DbUpdateException)
        {
            logger.LogWarning(ex, "Không xử lý được IPN từ SePay.");
            return BadRequest(new { success = false });
        }
    }

    [HttpPost("bank-webhook")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> BankWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var accepted = await sePayService.HandleBankWebhookAsync(
                rawBody,
                Request.Headers["X-SePay-Signature"].FirstOrDefault(),
                Request.Headers["X-SePay-Timestamp"].FirstOrDefault(),
                cancellationToken);

            return accepted ? Ok(new { success = true }) : Unauthorized(new { success = false });
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or DbUpdateException)
        {
            logger.LogWarning(ex, "Không xử lý được Bank Webhook từ SePay.");
            return BadRequest(new { success = false });
        }
    }

    [HttpGet("payments/{invoiceNumber}")]
    public async Task<IActionResult> GetPayment(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        if (!IsInternalRequestAuthorized())
            return Unauthorized(new { message = "Thiếu hoặc sai khóa truy cập dịch vụ thanh toán." });

        var payment = await db.Payments.AsNoTracking()
            .Where(x => x.InvoiceNumber == invoiceNumber)
            .Select(x => new
            {
                x.InvoiceNumber,
                x.Amount,
                x.Currency,
                x.Status,
                x.CreatedAt,
                x.PaidAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        return payment is null ? NotFound() : Ok(payment);
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = configuration["SEPAY_CHECKOUT_API_KEY"]
            ?? Environment.GetEnvironmentVariable("SEPAY_CHECKOUT_API_KEY");
        var supplied = Request.Headers["X-Payment-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    [HttpGet("return/{result}")]
    public IActionResult PaymentReturn(string result)
    {
        if (result is not ("success" or "error" or "cancel"))
            return NotFound();

        // Đây chỉ là URL điều hướng trình duyệt, không xác nhận giao dịch.
        return Ok(new
        {
            result,
            message = "Trạng thái thanh toán chính thức được xác nhận qua IPN/Webhook."
        });
    }
}
