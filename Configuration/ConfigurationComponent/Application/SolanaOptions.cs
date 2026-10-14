namespace ConfigurationComponent.Application;

/// <summary>
/// Represents Solana Options.
/// </summary>
public sealed class SolanaOptions
{
    public SolanaServiceMode Mode { get; set; } = SolanaServiceMode.Mock;

    public string RpcEndpoint { get; set; } = "https://api.devnet.solana.com";

    public string Commitment { get; set; } = "confirmed";

    public string? PayerPublicKey { get; set; }

    public string? PayerPrivateKeyBase58 { get; set; }

    public bool EnableAirdropHeartbeat { get; set; }

    public bool EnableProgramInvocation { get; set; }

    public bool SkipPreflight { get; set; }
}
