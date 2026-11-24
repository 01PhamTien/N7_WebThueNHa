using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Services.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/sepay")]
public sealed class SePayController(
    ISePayService sePayService,
    ILogger<SePayController> logger) : ControllerBase
{
    /// <summary>Tenant tạo phiên thanh toán SePay cho hóa đơn thuộc mình.</summary>
    [HttpPost("invoices/{invoiceId:guid}/checkout")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(SePayCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateCheckout(
        Guid invoiceId,
        [FromBody] CreateSePayCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var tenantId))
            return Unauthorized();

        if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount)
            return BadRequest(new { message = "Số tiền thanh toán phải là số VND nguyên dương." });

        try
        {
            var checkout = await sePayService.CreateCheckoutAsync(
                invoiceId, tenantId, request.Amount, cancellationToken);
            return Ok(checkout);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Không tìm thấy hóa đơn." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    /// <summary>IPN của SePay Gateway. Chỉ IPN đã xác thực mới cập nhật thanh toán.</summary>
    [HttpPost("ipn")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
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
            logger.LogError(exception, "Thiếu cấu hình nhận IPN SePay.");
            return Problem("Cổng thanh toán chưa được cấu hình đầy đủ.", statusCode: 503);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Không lưu được IPN SePay.");
            return Problem("Chưa thể ghi nhận IPN.", statusCode: 503);
        }
    }

    /// <summary>Webhook biến động số dư ngân hàng từ SePay.</summary>
    [HttpPost("bank-webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
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
            logger.LogError(exception, "Thiếu cấu hình Bank Webhook SePay.");
            return Problem("Webhook ngân hàng chưa được cấu hình đầy đủ.", statusCode: 503);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Không lưu được Bank Webhook SePay.");
            return Problem("Chưa thể ghi nhận webhook ngân hàng.", statusCode: 503);
        }
    }

    /// <summary>URL điều hướng trình duyệt; không xác nhận trạng thái giao dịch.</summary>
    [HttpGet("return/{result}")]
    [AllowAnonymous]
    public IActionResult PaymentReturn(string result)
    {
        if (result is not ("success" or "error" or "cancel"))
            return NotFound();

        return Ok(new
        {
            result,
            message = "Trạng thái thanh toán chính thức chỉ được xác nhận qua IPN hoặc Bank Webhook hợp lệ."
        });
    }
}
