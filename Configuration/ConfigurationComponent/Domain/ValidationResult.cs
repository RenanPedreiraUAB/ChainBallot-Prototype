namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Validation Result.
/// </summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Executes Success.
    /// </summary>
    public static ValidationResult Success() => new(true, []);

    /// <summary>
    /// Executes Failure.
    /// </summary>
    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult(false, errors);
    }
}
