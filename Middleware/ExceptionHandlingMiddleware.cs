using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web2_ThueNha.Middleware;

/// <summary>Xử lý lỗi chưa được xử lý và trả ProblemDetails thống nhất.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client đã ngắt kết nối; không cố ghi response hoặc coi đây là lỗi máy chủ.
        }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;
            if (environment.IsDevelopment())
            {
                logger.LogError(exception,
                    "Request thất bại. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                    traceId, context.Request.Method, context.Request.Path);
            }
            else
            {
                // Không ghi nội dung exception/SQL có khả năng chứa dữ liệu nhạy cảm ở Production.
                logger.LogError(
                    "Request thất bại. TraceId: {TraceId}, Method: {Method}, Path: {Path}, ExceptionType: {ExceptionType}",
                    traceId, context.Request.Method, context.Request.Path, exception.GetType().Name);
            }

            if (context.Response.HasStarted)
                throw;

            var (status, title, detail) = MapException(exception);
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = environment.IsDevelopment()
                    ? exception.Message
                    : detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = traceId;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
        }
    }

    private static (int Status, string Title, string SafeDetail) MapException(Exception exception) =>
        exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.",
                "Dữ liệu gửi lên không hợp lệ."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ.",
                "Yêu cầu gửi lên không hợp lệ."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy tài nguyên.",
                "Tài nguyên được yêu cầu không tồn tại."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Dữ liệu đã thay đổi.",
                "Dữ liệu đã được thay đổi bởi một yêu cầu khác. Vui lòng tải lại và thử lại."),
            _ => (StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi máy chủ.",
                "Máy chủ không thể xử lý yêu cầu. Vui lòng thử lại sau.")
        };
}
