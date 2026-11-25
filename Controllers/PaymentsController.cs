using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services;
using Web2_ThueNha.Services.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Người thuê gửi yêu cầu ghi nhận thanh toán tiền mặt hoặc chuyển khoản.</summary>
    [HttpPost("invoices/{invoiceId:guid}/payments")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePayment(
        Guid invoiceId,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await paymentService.CreatePaymentAsync(invoiceId, userId, request, cancellationToken));
    }

    /// <summary>Chủ nhà của hợp đồng hoặc Admin xác nhận khoản thanh toán đang chờ.</summary>
    [HttpPatch("payments/{paymentId:guid}/confirm")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(Guid paymentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await paymentService.ConfirmPaymentAsync(
            paymentId, userId, User.IsInRole("Admin"), cancellationToken));
    }

    /// <summary>Chủ nhà của hợp đồng hoặc Admin từ chối khoản thanh toán và lưu lý do.</summary>
    [HttpPatch("payments/{paymentId:guid}/reject")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectPayment(
        Guid paymentId,
        [FromBody] RejectPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await paymentService.RejectPaymentAsync(
            paymentId, userId, User.IsInRole("Admin"), request, cancellationToken));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Data),
            StatusCodes.Status201Created => StatusCode(StatusCodes.Status201Created, result.Data),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
            _ => StatusCode(result.StatusCode, result.Message is null ? null : new { message = result.Message })
        };

    public static PaymentResponse ToResponse(Payment payment) => PaymentService.ToResponse(payment);
}
