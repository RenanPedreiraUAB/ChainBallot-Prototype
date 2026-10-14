using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Enforces the Election Configuration Component's election rules (Architecture.md § 3.1): an invalid
/// draft can never reach metadata generation or deployment, which is what keeps published metadata
/// trustworthy for every downstream component.
/// </summary>
public sealed class ValidationService : IValidationService
{
    /// <summary>Validates the base election rules that must hold even for an unsaved draft.</summary>
    public ValidationResult ValidateForDraftSave(ElectionConfiguration election)
    {
        var errors = ValidateBase(election);
        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>Validates the base rules plus deployment-specific preconditions (Draft lifecycle state, minimum candidate count).</summary>
    public ValidationResult ValidateForDeployment(ElectionConfiguration election)
    {
        var errors = ValidateBase(election);

        if (election.LifecycleState != ElectionLifecycleState.Draft)
        {
            errors.Add("Only DRAFT elections can be deployed.");
        }

        if (election.Candidates.Count < 2)
        {
            errors.Add("At least two candidates are required.");
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    private static List<string> ValidateBase(ElectionConfiguration election)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(election.Name))
        {
            errors.Add("Election name is required.");
        }

        if (election.OpeningDate >= election.ClosingDate)
        {
            errors.Add("Opening time must be earlier than closing time.");
        }

        if (election.OpeningDate <= DateTimeOffset.UtcNow)
        {
            errors.Add("Opening date must be in the future.");
        }

        var duplicateCandidateName = election.Candidates
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCandidateName is not null)
        {
            errors.Add($"Candidate names must be unique. Duplicate: {duplicateCandidateName.Key}");
        }

        var duplicateCandidateId = election.Candidates
            .GroupBy(c => c.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCandidateId is not null)
        {
            errors.Add($"Candidate IDs must be unique. Duplicate ID: {duplicateCandidateId.Key}");
        }

        return errors;
    }
}
