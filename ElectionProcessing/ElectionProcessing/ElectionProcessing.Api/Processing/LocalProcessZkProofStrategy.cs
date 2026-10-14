using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class LocalProcessZkProofStrategy(
    IOptions<ProcessingOptions> options,
    ILogger<LocalProcessZkProofStrategy> logger) : IZkProofStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => ProverModes.LocalProcess;

    public async Task<ProofArtifact> GenerateProofAsync(BatchComputationInput input, CancellationToken cancellationToken)
    {
        var prover = options.Value.LocalProcessProver;
        if (string.IsNullOrWhiteSpace(prover.ExecutablePath))
        {
            throw new InvalidOperationException("Local process prover executable path is not configured.");
        }

        var request = new LocalProverRequest(
            input.ElectionId,
            input.BatchId,
            input.BatchSequence,
            input.MerkleRoot,
            input.Commitments,
            input.CreatedAtUtc);

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        var startInfo = new ProcessStartInfo
        {
            FileName = prover.ExecutablePath,
            Arguments = prover.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(prover.WorkingDirectory)
                ? Environment.CurrentDirectory
                : prover.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start local process prover.");
        }

        await process.StandardInput.WriteAsync(requestJson.AsMemory(), cancellationToken);
        await process.StandardInput.WriteLineAsync();
        process.StandardInput.Close();

        var timeout = TimeSpan.FromSeconds(prover.TimeoutSeconds);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));

        if (completedTask != waitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort process cleanup.
            }

            throw new TimeoutException($"Local prover process timed out after {prover.TimeoutSeconds} seconds.");
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Local prover exited with code {process.ExitCode}: {error}");
        }

        var payload = JsonSerializer.Deserialize<LocalProverResponse>(output, JsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException("Local prover returned an empty response payload.");
        }

        if (string.IsNullOrWhiteSpace(payload.ProofReference))
        {
            throw new InvalidOperationException("Local prover response is missing proofReference.");
        }

        var digest = string.IsNullOrWhiteSpace(payload.ProofDigest)
            ? ComputeDigest(request, payload.ProofReference)
            : payload.ProofDigest.Trim().ToLowerInvariant();

        logger.LogInformation("Local process prover generated proof for election {ElectionId}, batch {BatchId}.", input.ElectionId, input.BatchId);

        return new ProofArtifact(
            ProofSystem: string.IsNullOrWhiteSpace(payload.ProofSystem) ? Name : payload.ProofSystem.Trim(),
            ProofReference: payload.ProofReference.Trim(),
            ProofDigest: digest,
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string ComputeDigest(LocalProverRequest request, string proofReference)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            request.ElectionId,
            request.BatchId,
            request.BatchSequence,
            request.MerkleRoot,
            request.Commitments,
            ProofReference = proofReference
        }, JsonOptions);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record LocalProverRequest(
        string ElectionId,
        string BatchId,
        int BatchSequence,
        string MerkleRoot,
        IReadOnlyList<string> Commitments,
        DateTimeOffset CreatedAtUtc);

    private sealed record LocalProverResponse(
        string? ProofSystem,
        string? ProofReference,
        string? ProofDigest);
}
