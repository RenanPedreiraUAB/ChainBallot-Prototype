using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines validation rules for election draft persistence and deployment readiness.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates an election draft before it is saved.
    /// </summary>
    /// <param name="election">Election to validate.</param>
    /// <returns>Validation result containing success state and errors.</returns>
    ValidationResult ValidateForDraftSave(ElectionConfiguration election);

    /// <summary>
    /// Validates an election before deployment.
    /// </summary>
    /// <param name="election">Election to validate.</param>
    /// <returns>Validation result containing success state and errors.</returns>
    ValidationResult ValidateForDeployment(ElectionConfiguration election);
}
