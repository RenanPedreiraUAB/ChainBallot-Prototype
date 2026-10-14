using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChainBallot.PublicContracts;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Ballot confidentiality realization for the Voter Component (Architecture.md § 6.2): encrypts the
/// ballot draft with AES-GCM (authenticated encryption) before it ever leaves the component, so the
/// message broker and Election Processing Component only ever observe ciphertext.
/// The symmetric key is derived from the configured <c>BallotEncryptionKey</c> via SHA-256.
/// </summary>
public sealed class AesGcmBallotEncryptionService(IOptions<VoterOptions> options) : IBallotEncryptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public EncryptedBallotPayload Encrypt(BallotSubmissionDraft draft)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(draft, JsonOptions);
        var key = DeriveKey(options.Value.BallotEncryptionKey);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return new EncryptedBallotPayload(
            "A256GCM",
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    private static byte[] DeriveKey(string rawKey)
    {
        var seed = Encoding.UTF8.GetBytes(rawKey);
        return SHA256.HashData(seed);
    }
}