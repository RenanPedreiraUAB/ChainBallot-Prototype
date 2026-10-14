using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class ExternalHttpZkProofStrategy(
    HttpClient httpClient,
    IOptions<ProcessingOptions> options) : IZkProofStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => ProverModes.ExternalHttp;

    public async Task<ProofArtifact> GenerateProofAsync(BatchComputationInput input, CancellationToken cancellationToken)
    {
        var endpoint = options.Value.ExternalProver.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("External prover endpoint is not configured.");
        }

        var request = new ExternalProverRequest(
            input.ElectionId,
            input.BatchId,
            input.BatchSequence,
            input.MerkleRoot,
            input.Commitments,
            input.CreatedAtUtc);

        using var response = await httpClient.PostAsJsonAsync(endpoint, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ExternalProverResponse>(JsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("External prover returned an empty response.");
        }

        if (string.IsNullOrWhiteSpace(payload.ProofReference))
        {
            throw new InvalidOperationException("External prover response is missing proofReference.");
        }

        var digest = string.IsNullOrWhiteSpace(payload.ProofDigest)
            ? ComputeDigest(request, payload.ProofReference)
            : payload.ProofDigest.Trim().ToLowerInvariant();

        return new ProofArtifact(
            ProofSystem: string.IsNullOrWhiteSpace(payload.ProofSystem) ? Name : payload.ProofSystem.Trim(),
            ProofReference: payload.ProofReference.Trim(),
            ProofDigest: digest,
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string ComputeDigest(ExternalProverRequest request, string proofReference)
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

    private sealed record ExternalProverRequest(
        string ElectionId,
        string BatchId,
        int BatchSequence,
        string MerkleRoot,
        IReadOnlyList<string> Commitments,
        DateTimeOffset CreatedAtUtc);

    private sealed record ExternalProverResponse(
        string? ProofSystem,
        string? ProofReference,
        string? ProofDigest);
}
