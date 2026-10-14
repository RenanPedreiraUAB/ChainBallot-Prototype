using ThresholdDecryption.Api.Domain;

namespace ThresholdDecryption.Api.Services;

public interface IThresholdCeremonyRepository
{
    Task<ThresholdCeremony> SaveAsync(ThresholdCeremony ceremony, CancellationToken cancellationToken);

    Task<ThresholdCeremony?> GetByIdAsync(string ceremonyId, CancellationToken cancellationToken);

    Task<ThresholdCeremony?> GetByElectionAsync(string electionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ThresholdCeremony>> ListByElectionAsync(string electionId, CancellationToken cancellationToken);
}
