namespace ElectionAuthorization.Api.Security;

/// <summary>
/// Defines VID generation for authenticated subjects.
/// </summary>
public interface IVidGenerator
{
    /// <summary>
    /// Creates a new VID value.
    /// </summary>
    /// <returns>Generated VID identifier.</returns>
    string GenerateVid();
}
