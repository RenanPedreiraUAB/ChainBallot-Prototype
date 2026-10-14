namespace ResultPortal.Web.Services;

public enum ElectionAccessDenyMode
{
    Challenge = 0,
    Forbid = 1,
    NotFound = 2
}

public sealed record ElectionAccessDecision(
    bool IsAllowed,
    ElectionAccessDenyMode? DenyMode,
    string? Message)
{
    public static ElectionAccessDecision Allow() => new(true, null, null);

    public static ElectionAccessDecision Deny(ElectionAccessDenyMode mode, string message)
        => new(false, mode, message);
}
