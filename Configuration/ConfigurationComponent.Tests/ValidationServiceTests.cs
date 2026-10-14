using ConfigurationComponent.Domain;
using ConfigurationComponent.Services;
using ConfigurationComponent.Tests.Helpers;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Validation Service Tests.
/// </summary>
public sealed class ValidationServiceTests
{
    private readonly ValidationService _service = new();

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_With Valid Draft_Returns Success.
    /// </summary>
    public void ValidateForDeployment_WithValidDraft_ReturnsSuccess()
    {
        var election = TestDataFactory.CreateValidDraftElection();

        var result = _service.ValidateForDeployment(election);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Name Missing_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenNameMissing_ReturnsError()
    {
        var election = TestDataFactory.CreateValidDraftElection() with { Name = "   " };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains("Election name is required.", result.Errors);
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Opening Not Future_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenOpeningNotFuture_ReturnsError()
    {
        var now = DateTimeOffset.UtcNow;
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            OpeningDate = now.AddMinutes(-10),
            ClosingDate = now.AddHours(2)
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains("Opening date must be in the future.", result.Errors);
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Dates Invalid_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenDatesInvalid_ReturnsError()
    {
        var now = DateTimeOffset.UtcNow;
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            OpeningDate = now.AddDays(2),
            ClosingDate = now.AddDays(1)
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains("Opening time must be earlier than closing time.", result.Errors);
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Less Than Two Candidates_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenLessThanTwoCandidates_ReturnsError()
    {
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            Candidates = [new Candidate(1, "Alice", "Party A", "Candidate A", "https://img/a.png", 1)]
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains("At least two candidates are required.", result.Errors);
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Duplicate Candidate Name_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenDuplicateCandidateName_ReturnsError()
    {
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            Candidates =
            [
                new Candidate(1, "Alice", "Party A", "Candidate A", "https://img/a.png", 1),
                new Candidate(2, "alice", "Party B", "Candidate B", "https://img/b.png", 2)
            ]
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("Candidate names must be unique."));
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Duplicate Candidate Id_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenDuplicateCandidateId_ReturnsError()
    {
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            Candidates =
            [
                new Candidate(1, "Alice", "Party A", "Candidate A", "https://img/a.png", 1),
                new Candidate(1, "Bob", "Party B", "Candidate B", "https://img/b.png", 2)
            ]
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("Candidate IDs must be unique."));
    }

    [Fact]
    /// <summary>
    /// Executes Validate For Deployment_When Not Draft_Returns Error.
    /// </summary>
    public void ValidateForDeployment_WhenNotDraft_ReturnsError()
    {
        var election = TestDataFactory.CreateValidDraftElection() with
        {
            LifecycleState = ElectionLifecycleState.Deployed
        };

        var result = _service.ValidateForDeployment(election);

        Assert.False(result.IsValid);
        Assert.Contains("Only DRAFT elections can be deployed.", result.Errors);
    }
}
