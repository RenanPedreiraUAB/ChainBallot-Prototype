namespace ResultPortal.Web.Services;

public enum TransparencyReadStatus
{
    Success = 0,
    NotFound = 1,
    Unauthorized = 2,
    Unavailable = 3,
    Error = 4
}

public sealed record TransparencyReadResult<T>(
    TransparencyReadStatus Status,
    T? Value,
    string? Message,
    string CorrelationId)
{
    public static TransparencyReadResult<T> Success(T value, string correlationId)
        => new(TransparencyReadStatus.Success, value, null, correlationId);

    public static TransparencyReadResult<T> NotFound(string correlationId, string? message = null)
        => new(TransparencyReadStatus.NotFound, default, message, correlationId);

    public static TransparencyReadResult<T> Unauthorized(string correlationId, string? message = null)
        => new(TransparencyReadStatus.Unauthorized, default, message, correlationId);

    public static TransparencyReadResult<T> Unavailable(string correlationId, string? message = null)
        => new(TransparencyReadStatus.Unavailable, default, message, correlationId);

    public static TransparencyReadResult<T> Error(string correlationId, string? message = null)
        => new(TransparencyReadStatus.Error, default, message, correlationId);
}
