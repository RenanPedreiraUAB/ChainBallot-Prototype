namespace ElectionProcessing.Api.Processing;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class UtcSystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
