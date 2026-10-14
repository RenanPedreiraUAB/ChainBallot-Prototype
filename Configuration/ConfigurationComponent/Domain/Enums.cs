namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Election Lifecycle State.
/// </summary>
public enum ElectionLifecycleState
{
    Draft = 0,
    Deployed = 1,
    Open = 2,
    Closed = 3
}

/// <summary>
/// Represents Deployment Status.
/// </summary>
public enum DeploymentStatus
{
    NotStarted = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3
}

/// <summary>
/// Represents Election Type.
/// </summary>
public enum ElectionType
{
    SingleChoice = 0,
    MultipleChoice = 1
}

/// <summary>
/// Represents Solana Network.
/// </summary>
public enum SolanaNetwork
{
    Devnet = 0,
    Testnet = 1,
    Mainnet = 2
}
