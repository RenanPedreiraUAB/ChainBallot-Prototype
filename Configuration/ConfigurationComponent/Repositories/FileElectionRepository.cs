using System.Text.Json;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Utilities;

namespace ConfigurationComponent.Repositories;

/// <summary>
/// Represents File Election Repository.
/// </summary>
public sealed class FileElectionRepository : IElectionRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Create();

    public FileElectionRepository()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChainBallot",
            "ConfigurationComponent");

        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "elections.json");
    }

    /// <summary>
    /// Executes Get All Async.
    /// </summary>
    public async Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var elections = await ReadAllInternalAsync(cancellationToken);
            return elections.OrderByDescending(e => e.CreatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Executes Get By Id Async.
    /// </summary>
    public async Task<ElectionConfiguration?> GetByIdAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var elections = await ReadAllInternalAsync(cancellationToken);
            return elections.FirstOrDefault(x => x.Id == electionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Executes Save Async.
    /// </summary>
    public async Task SaveAsync(ElectionConfiguration election, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var elections = await ReadAllInternalAsync(cancellationToken);
            var existingIndex = elections.FindIndex(e => e.Id == election.Id);
            if (existingIndex >= 0)
            {
                elections[existingIndex] = election;
            }
            else
            {
                elections.Add(election);
            }

            await WriteAllInternalAsync(elections, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Executes Delete Async.
    /// </summary>
    public async Task DeleteAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var elections = await ReadAllInternalAsync(cancellationToken);
            elections.RemoveAll(e => e.Id == electionId);
            await WriteAllInternalAsync(elections, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ElectionConfiguration>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var elections = await JsonSerializer.DeserializeAsync<List<ElectionConfiguration>>(stream, _jsonOptions, cancellationToken);
        return elections ?? [];
    }

    private async Task WriteAllInternalAsync(List<ElectionConfiguration> elections, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, elections, _jsonOptions, cancellationToken);
    }
}
