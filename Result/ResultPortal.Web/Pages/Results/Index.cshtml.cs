using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResultPortal.Web.Services;

namespace ResultPortal.Web.Pages.Results;

/// <summary>
/// Result Portal page realizing UIS-06's read/publication phase (Architecture.md § 3.9): evaluates the
/// election's visibility policy, then renders the verified snapshot sourced exclusively from the
/// Transparency &amp; Audit Component. Holds no independent repository of its own.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly ITransparencyReadClient _transparencyReadClient;
    private readonly IElectionAccessPolicy _electionAccessPolicy;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ITransparencyReadClient transparencyReadClient,
        IElectionAccessPolicy electionAccessPolicy,
        ILogger<IndexModel> logger)
    {
        _transparencyReadClient = transparencyReadClient;
        _electionAccessPolicy = electionAccessPolicy;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string ElectionId { get; set; } = string.Empty;

    public VerifiedResultSnapshot? Snapshot { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? InfoMessage { get; private set; }

    public string? CorrelationId { get; private set; }

    public bool HasResult => Snapshot is not null;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ElectionId))
        {
            InfoMessage = "Provide an election id to load its verified snapshot.";
            return Page();
        }

        var accessDecision = _electionAccessPolicy.EvaluateResultsAccess(ElectionId, User);
        if (!accessDecision.IsAllowed)
        {
            return ToActionResult(accessDecision);
        }

        try
        {
            var readResult = await _transparencyReadClient.GetSnapshotAsync(ElectionId, cancellationToken).ConfigureAwait(false);
            CorrelationId = readResult.CorrelationId;

            switch (readResult.Status)
            {
                case TransparencyReadStatus.Success:
                    Snapshot = readResult.Value;
                    break;
                case TransparencyReadStatus.NotFound:
                    ErrorMessage = "No verified snapshot was found for this election id.";
                    break;
                case TransparencyReadStatus.Unauthorized:
                    ErrorMessage = "Result visibility policy denied this request.";
                    break;
                case TransparencyReadStatus.Unavailable:
                    ErrorMessage = "Transparency service is currently unavailable. Try again in a moment.";
                    break;
                default:
                    ErrorMessage = "Transparency response could not be processed.";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load verified snapshot. ElectionId={ElectionId}", ElectionId);
            ErrorMessage = "Failed to load verified snapshot.";
        }

        return Page();
    }

    private IActionResult ToActionResult(ElectionAccessDecision decision)
    {
        ErrorMessage = decision.Message;

        return decision.DenyMode switch
        {
            ElectionAccessDenyMode.NotFound => NotFound(),
            ElectionAccessDenyMode.Challenge => Challenge(),
            ElectionAccessDenyMode.Forbid => Forbid(),
            _ => Forbid()
        };
    }
}
