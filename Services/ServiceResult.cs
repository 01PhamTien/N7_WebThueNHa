namespace Web2_ThueNha.Services;

public sealed record ServiceResult<T>(int StatusCode, T? Data = default, string? Message = null)
{
    public static ServiceResult<T> Success(T data) => new(StatusCodes.Status200OK, data);
    public static ServiceResult<T> Created(T data) => new(StatusCodes.Status201Created, data);
    public static ServiceResult<T> BadRequest(string message) => new(StatusCodes.Status400BadRequest, default, message);
    public static ServiceResult<T> Forbidden() => new(StatusCodes.Status403Forbidden);
    public static ServiceResult<T> NotFound(string message) => new(StatusCodes.Status404NotFound, default, message);
    public static ServiceResult<T> Conflict(string message) => new(StatusCodes.Status409Conflict, default, message);
}
