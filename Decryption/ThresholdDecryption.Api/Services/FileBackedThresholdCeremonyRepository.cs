using System.Text.Json;
using ThresholdDecryption.Api.Domain;

namespace ThresholdDecryption.Api.Services;

public sealed class FileBackedThresholdCeremonyRepository : IThresholdCeremonyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileBackedThresholdCeremonyRepository(string path)
    {
        _path = path;
    }

    public async Task<ThresholdCeremony> SaveAsync(ThresholdCeremony ceremony, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            var updated = all.Where(c => !string.Equals(c.CeremonyId, ceremony.CeremonyId, StringComparison.OrdinalIgnoreCase)).ToList();
            updated.Add(ceremony);
            await WriteAllAsync(updated, cancellationToken).ConfigureAwait(false);
            return ceremony;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThresholdCeremony?> GetByIdAsync(string ceremonyId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return all.FirstOrDefault(c => string.Equals(c.CeremonyId, ceremonyId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThresholdCeremony?> GetByElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return all
                .Where(c => string.Equals(c.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.OpenedAtUtc)
                .FirstOrDefault();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ThresholdCeremony>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return all
                .Where(c => string.Equals(c.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.OpenedAtUtc)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ThresholdCeremony>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = File.OpenRead(_path);
        var records = await JsonSerializer.DeserializeAsync<List<ThresholdCeremony>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return records ?? [];
    }

    private async Task WriteAllAsync(IReadOnlyList<ThresholdCeremony> ceremonies, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, ceremonies, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
