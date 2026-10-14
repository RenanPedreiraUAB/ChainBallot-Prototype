using ConfigurationComponent.Application;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Repositories;

namespace ConfigurationComponent.Services;

/// <summary>
/// Manages election drafts through the Election Configuration Component's local repository
/// (Architecture.md § 3.1): creation, retrieval and persistence of elections prior to (and across)
/// the Draft-to-Deployed lifecycle transition.
/// </summary>
public sealed class ElectionService(
    IElectionRepository electionRepository,
    ProvisioningDefaults defaults) : IElectionService
{
    private readonly IElectionRepository _electionRepository = electionRepository;
    private readonly ProvisioningDefaults _defaults = defaults;

    /// <summary>
    /// Executes Get All Async.
    /// </summary>
    public Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _electionRepository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Create Draft Async.
    /// </summary>
    public async Task<ElectionConfiguration> CreateDraftAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var election = new ElectionConfiguration(
            Id: Guid.NewGuid(),
            Name: _defaults.NewElectionName,
            Description: string.Empty,
            OpeningDate: now.AddDays(Math.Max(1, _defaults.OpeningOffsetDays)),
            ClosingDate: now.AddDays(Math.Max(2, _defaults.ClosingOffsetDays)),
            ElectionType: ElectionType.SingleChoice,
            Candidates: [],
            DeploymentSettings: new DeploymentSettings(
                Network: _defaults.Network,
                ProgramId: _defaults.ProgramId,
                MetadataEndpoint: _defaults.MetadataEndpoint,
                ExpectedVoters: _defaults.ExpectedVoters,
                PeakParticipationPercent: _defaults.PeakParticipationPercent,
                ElectionDurationHours: _defaults.ElectionDurationHours,
                ProgramVersion: _defaults.ProgramVersion,
                BatchTreeType: _defaults.BatchTreeType,
                ProofSystem: _defaults.ProofSystem,
                DecryptionMode: _defaults.DecryptionMode),
            LifecycleState: ElectionLifecycleState.Draft,
            DeploymentStatus: DeploymentStatus.NotStarted,
            MetadataUri: null,
            MetadataHash: null,
            ElectionPda: null,
            DeploymentSignature: null,
            DeploymentManifestPath: null,
            CreatedAt: now,
            UpdatedAt: now);

        await _electionRepository.SaveAsync(election, cancellationToken);
        return election;
    }

    /// <summary>
    /// Executes Save Draft Async.
    /// </summary>
    public async Task<ElectionConfiguration> SaveDraftAsync(ElectionConfiguration election, CancellationToken cancellationToken = default)
    {
        if (!election.IsDraft)
        {
            throw new InvalidOperationException("Only draft elections can be modified.");
        }

        var updated = election with { UpdatedAt = DateTimeOffset.UtcNow };
        await _electionRepository.SaveAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Executes Delete Draft Async.
    /// </summary>
    public async Task DeleteDraftAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        var existing = await _electionRepository.GetByIdAsync(electionId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        if (!existing.IsDraft)
        {
            throw new InvalidOperationException("Only draft elections can be deleted.");
        }

        await _electionRepository.DeleteAsync(electionId, cancellationToken);
    }
}
