namespace VoterComponent.Web.Application;

public sealed class VoterOptions
{
    public const string SectionName = "Voter";

    public string CookieName { get; set; } = ".ChainBallot.Voter.Session";

    public string CynetAuthBaseUrl { get; set; } = "https://localhost:5081/";

    public string ElectionAuthorizationBaseUrl { get; set; } = "https://localhost:5091/";

    public string MetadataEndpointTemplate { get; set; } = "https://metadata.example/api/elections/{electionId}.json";

    public string DefaultElectionId { get; set; } = "election-demo";

    public string QueueMode { get; set; } = VoterQueueModes.Kafka;

    public VoterKafkaOptions Kafka { get; set; } = new();

    public string BallotEncryptionKey { get; set; } = "CHANGE_ME_VOTER_BALLOT_ENCRYPTION_KEY_1234567890";

    public string? SubmissionOutboxPath { get; set; }

    public int SubmissionOutboxEntryTtlMinutes { get; set; } = 1440;

    public int SubmissionOutboxMaxRetryCount { get; set; } = 10;

    public int SubmissionOutboxMaxEntries { get; set; } = 1000;
}

public static class VoterQueueModes
{
    public const string Kafka = "Kafka";

    public const string InMemory = "InMemory";
}

public sealed class VoterKafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";

    public string Topic { get; set; } = "chainballot-ballots";
}