using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Services;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api")]
public sealed class SePayController(
    ISePayService sePayService,
    ILogger<SePayController> logger) : ControllerBase
{
    /// <summary>Tenant tạo checkout SePay cho số dư còn lại của hóa đơn thuộc mình.</summary>
    [HttpPost("invoices/{invoiceId:guid}/sepay-checkout")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(SePayCheckout), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateCheckout(Guid invoiceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var tenantId))
            return Unauthorized();

        try
        {
            var checkout = await sePayService.CreateCheckoutAsync(invoiceId, tenantId, cancellationToken);
            return Ok(checkout);
        }
        catch (SePayRequestException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Cấu hình SePay chưa đầy đủ.");
            return Problem("Cổng thanh toán SePay chưa được cấu hình đầy đủ.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>IPN Gateway; chỉ callback hợp lệ mới được cập nhật Payment/Invoice.</summary>
    [HttpPost("sepay/ipn")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GatewayIpn(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var accepted = await sePayService.HandleGatewayIpnAsync(
                rawBody,
                Request.Headers["X-Secret-Key"].FirstOrDefault(),
                cancellationToken);

            return accepted
                ? Ok(new { success = true })
                : Unauthorized(new { success = false });
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "IPN SePay có JSON không hợp lệ.");
            return BadRequest(new { success = false });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Thiếu cấu hình xác thực IPN SePay.");
            return Problem("IPN SePay chưa được cấu hình.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Lỗi lưu IPN SePay; yêu cầu cần được gửi lại.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false });
        }
    }

    /// <summary>Bank Webhook xác thực HMAC trên raw body trước khi đối soát giao dịch vào.</summary>
    [HttpPost("sepay/bank-webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

            return accepted
                ? Ok(new { success = true })
                : Unauthorized(new { success = false });
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Bank Webhook SePay có JSON không hợp lệ.");
            return BadRequest(new { success = false });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Thiếu cấu hình xác thực Bank Webhook SePay.");
            return Problem("Bank Webhook SePay chưa được cấu hình.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Lỗi lưu Bank Webhook SePay; yêu cầu cần được gửi lại.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false });
        }
    }

    /// <summary>URL quay lại từ trình duyệt chỉ hiển thị thông tin; không xác nhận thanh toán.</summary>
    [HttpGet("sepay/return/{result}")]
    [AllowAnonymous]
    public IActionResult PaymentReturn(string result)
    {
        if (result is not ("success" or "error" or "cancel"))
            return NotFound();

        return Ok(new
        {
            result,
            message = "Trạng thái thanh toán chính thức được xác nhận qua IPN hoặc Bank Webhook."
        });
    }
}
