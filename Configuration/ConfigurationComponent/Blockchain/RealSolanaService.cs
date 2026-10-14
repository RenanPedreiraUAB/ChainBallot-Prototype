using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConfigurationComponent.Application;
using ConfigurationComponent.Domain;
using Microsoft.Extensions.Logging;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;

namespace ConfigurationComponent.Blockchain;

/// <summary>
/// Represents Real Solana Service.
/// </summary>
public sealed class RealSolanaService(SolanaOptions options, ILogger<RealSolanaService> logger) : ISolanaService
{
    private readonly SolanaOptions _options = options;
    private readonly ILogger<RealSolanaService> _logger = logger;
    private readonly Dictionary<Guid, ElectionLifecycleState> _states = [];

    /// <summary>
    /// Executes Deploy Election Async.
    /// </summary>
    public Task<DeploymentResult> DeployElectionAsync(
        ElectionConfiguration election,
        string metadataHash,
        string metadataUri,
        CancellationToken cancellationToken = default)
    {
        return DeployElectionInternalAsync(election, metadataHash, metadataUri, cancellationToken);
    }

    /// <summary>
    /// Executes Initialize Election Async.
    /// </summary>
    public async Task InitializeElectionAsync(ElectionConfiguration election, string metadataHash, CancellationToken cancellationToken = default)
    {
        _ = metadataHash;

        await EnsureRpcReadyAsync(cancellationToken);
        _states[election.Id] = ElectionLifecycleState.Deployed;
    }

    /// <summary>
    /// Executes Activate Election Async.
    /// </summary>
    public async Task ActivateElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        await EnsureRpcReadyAsync(cancellationToken);
        _states[electionId] = ElectionLifecycleState.Open;
    }

    /// <summary>
    /// Executes Close Election Async.
    /// </summary>
    public async Task CloseElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        await EnsureRpcReadyAsync(cancellationToken);
        _states[electionId] = ElectionLifecycleState.Closed;
    }

    /// <summary>
    /// Executes Get Election Status Async.
    /// </summary>
    public async Task<ElectionLifecycleState> GetElectionStatusAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        await EnsureRpcReadyAsync(cancellationToken);
        return _states.TryGetValue(electionId, out var state)
            ? state
            : ElectionLifecycleState.Draft;
    }

    private async Task<DeploymentResult> DeployElectionInternalAsync(
        ElectionConfiguration election,
        string metadataHash,
        string metadataUri,
        CancellationToken cancellationToken)
    {
        var blockhash = await EnsureRpcReadyAsync(cancellationToken);
        var pda = $"pda_{election.Id:N}";

        string signature;
        if (_options.EnableProgramInvocation)
        {
            signature = await SubmitInitializationTransactionAsync(election, metadataHash, metadataUri, cancellationToken);
            _logger.LogInformation(
                "Program invocation transaction submitted for election {ElectionId}. Signature: {Signature}",
                election.Id,
                signature);
        }
        else if (_options.EnableAirdropHeartbeat && !string.IsNullOrWhiteSpace(_options.PayerPublicKey))
        {
            signature = await RequestAirdropHeartbeatAsync(_options.PayerPublicKey, cancellationToken);
            _logger.LogInformation(
                "RPC heartbeat transaction submitted for election {ElectionId}. Signature: {Signature}",
                election.Id,
                signature);
        }
        else
        {
            signature = $"rpc_preflight_{blockhash[..Math.Min(20, blockhash.Length)]}_{Guid.NewGuid():N}";
            _logger.LogWarning(
                "RealSolanaService is operating in preflight mode (no signed program transaction configured). Election {ElectionId} got synthetic signature {Signature}",
                election.Id,
                signature);
        }

        _states[election.Id] = ElectionLifecycleState.Deployed;

        return new DeploymentResult(
            ProgramId: election.DeploymentSettings.ProgramId,
            ElectionPda: pda,
            MetadataUri: metadataUri,
            TransactionSignature: signature,
            MetadataHash: metadataHash);
    }

    private async Task<string> SubmitInitializationTransactionAsync(
        ElectionConfiguration election,
        string metadataHash,
        string metadataUri,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PayerPrivateKeyBase58))
        {
            throw new InvalidOperationException("Solana program invocation requires Solana:PayerPrivateKeyBase58.");
        }

        var payer = BuildPayerAccount(_options.PayerPrivateKeyBase58);
        var rpc = ClientFactory.GetClient(_options.RpcEndpoint);
        var commitment = ParseCommitment(_options.Commitment);

        var latestBlockHash = await rpc.GetLatestBlockHashAsync(commitment);
        EnsureRpcSuccess(latestBlockHash, "getLatestBlockhash");

        var programId = new PublicKey(election.DeploymentSettings.ProgramId);
        var data = BuildInitializationPayload(election, metadataHash, metadataUri);

        var instruction = new TransactionInstruction
        {
            ProgramId = programId,
            Keys =
            [
                AccountMeta.Writable(payer.PublicKey, true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
            ],
            Data = data
        };

        var tx = new TransactionBuilder()
            .SetRecentBlockHash(latestBlockHash.Result.Value.Blockhash)
            .SetFeePayer(payer)
            .AddInstruction(instruction)
            .Build(payer);

        var sendResult = await rpc.SendTransactionAsync(
            tx,
            _options.SkipPreflight,
            commitment);

        EnsureRpcSuccess(sendResult, "sendTransaction");

        if (string.IsNullOrWhiteSpace(sendResult.Result))
        {
            throw new InvalidOperationException("sendTransaction succeeded but did not return a signature.");
        }

        return sendResult.Result;
    }

    private async Task<string> EnsureRpcReadyAsync(CancellationToken cancellationToken)
    {
        var endpoint = _options.RpcEndpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Solana RPC endpoint is not configured. Set Solana:RpcEndpoint.");
        }

        using var httpClient = new HttpClient();

        // 1) Ensure node responds.
        _ = await CallRpcAsync<JsonElement>(httpClient, endpoint, "getHealth", null, cancellationToken);

        // 2) Fetch a recent blockhash for preflight liveness.
        var latestBlockhashResult = await CallRpcAsync<JsonElement>(
            httpClient,
            endpoint,
            "getLatestBlockhash",
            new object[] { new { commitment = _options.Commitment } },
            cancellationToken);

        var value = latestBlockhashResult.GetProperty("value");
        var blockhash = value.GetProperty("blockhash").GetString();
        if (string.IsNullOrWhiteSpace(blockhash))
        {
            throw new InvalidOperationException("Solana RPC did not return a valid blockhash.");
        }

        return blockhash;
    }

    private async Task<string> RequestAirdropHeartbeatAsync(string payerPublicKey, CancellationToken cancellationToken)
    {
        var endpoint = _options.RpcEndpoint.Trim();
        using var httpClient = new HttpClient();

        var signature = await CallRpcAsync<string>(
            httpClient,
            endpoint,
            "requestAirdrop",
            new object[]
            {
                payerPublicKey,
                1,
                new { commitment = _options.Commitment }
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new InvalidOperationException("Solana RPC heartbeat request did not return a signature.");
        }

        return signature;
    }

    private static Account BuildPayerAccount(string privateKeyBase58)
    {
        var secret = Encoders.Base58.DecodeData(privateKeyBase58.Trim());
        if (secret.Length < 64)
        {
            throw new InvalidOperationException("Solana:PayerPrivateKeyBase58 must decode to a 64-byte secret key.");
        }

        var privateKey = secret.Take(32).ToArray();
        var publicKey = secret.Skip(32).Take(32).ToArray();
        return new Account(privateKey, publicKey);
    }

    private static byte[] BuildInitializationPayload(
        ElectionConfiguration election,
        string metadataHash,
        string metadataUri)
    {
        var payload = new
        {
            action = "initialize-election",
            electionId = election.Id.ToString("N"),
            openingTs = election.OpeningDate.ToUnixTimeSeconds(),
            closingTs = election.ClosingDate.ToUnixTimeSeconds(),
            candidateIds = election.Candidates
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Id)
                .ToArray(),
            metadataHash,
            metadataUri,
            programVersion = election.DeploymentSettings.ProgramVersion,
            policy = new
            {
                batchTreeType = election.DeploymentSettings.BatchTreeType,
                proofSystem = election.DeploymentSettings.ProofSystem,
                decryptionMode = election.DeploymentSettings.DecryptionMode
            }
        };

        var json = JsonSerializer.Serialize(payload);
        return Encoding.UTF8.GetBytes(json);
    }

    private static Commitment ParseCommitment(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "processed" => Commitment.Processed,
            "finalized" => Commitment.Finalized,
            _ => Commitment.Confirmed
        };
    }

    private static void EnsureRpcSuccess<T>(RequestResult<T> response, string method)
    {
        if (response.WasSuccessful)
        {
            return;
        }

        var details = $"code={response.ServerErrorCode}; reason={response.Reason}; data={response.ErrorData}";
        throw new InvalidOperationException($"Solana RPC call '{method}' failed: {details}");
    }

    private static async Task<T> CallRpcAsync<T>(
        HttpClient client,
        string endpoint,
        string method,
        object[]? @params,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params
        };

        using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rpcResponse = await response.Content.ReadFromJsonAsync<SolanaRpcEnvelope<T>>(cancellationToken: cancellationToken);
        if (rpcResponse is null)
        {
            throw new InvalidOperationException($"Solana RPC returned an empty response for method '{method}'.");
        }

        if (rpcResponse.Error is not null)
        {
            throw new InvalidOperationException($"Solana RPC error for method '{method}': {rpcResponse.Error.Code} - {rpcResponse.Error.Message}");
        }

        if (rpcResponse.Result is null)
        {
            throw new InvalidOperationException($"Solana RPC response for method '{method}' does not contain a result.");
        }

        return rpcResponse.Result;
    }

    private sealed class SolanaRpcEnvelope<T>
    {
        public T? Result { get; init; }

        public SolanaRpcError? Error { get; init; }
    }

    private sealed class SolanaRpcError
    {
        public int Code { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
