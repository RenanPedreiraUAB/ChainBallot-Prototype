using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines generation and serialization of deployment handoff manifests.
/// </summary>
public interface IDeploymentManifestGenerator
{
    /// <summary>
    /// Builds a deployment manifest from election and blockchain deployment data.
    /// </summary>
    /// <param name="election">Election configuration source.</param>
    /// <param name="deploymentResult">Blockchain deployment output.</param>
    /// <param name="metadataCreationTimestamp">Canonical metadata creation timestamp.</param>
    /// <param name="generatedAtTimestamp">Manifest generation timestamp.</param>
    /// <returns>Structured deployment manifest document.</returns>
    DeploymentManifest BuildManifest(
        ElectionConfiguration election,
        DeploymentResult deploymentResult,
        long metadataCreationTimestamp,
        long generatedAtTimestamp);

    /// <summary>
    /// Serializes a deployment manifest to JSON.
    /// </summary>
    /// <param name="manifest">Manifest to serialize.</param>
    /// <returns>Serialized JSON string.</returns>
    string Serialize(DeploymentManifest manifest);
}
