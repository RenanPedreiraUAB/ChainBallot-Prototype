namespace ConfigurationComponent.Repositories;

/// <summary>
/// Represents File Deployment Manifest Repository.
/// </summary>
public sealed class FileDeploymentManifestRepository : IDeploymentManifestRepository
{
    private readonly string _directoryPath;

    public FileDeploymentManifestRepository()
    {
        _directoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChainBallot",
            "ConfigurationComponent",
            "deployment-manifests");

        Directory.CreateDirectory(_directoryPath);
    }

    /// <summary>
    /// Executes Save Async.
    /// </summary>
    public async Task<string> SaveAsync(string electionId, string manifestJson, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_directoryPath, $"{electionId}.manifest.json");
        await File.WriteAllTextAsync(filePath, manifestJson, cancellationToken);
        return filePath;
    }
}
