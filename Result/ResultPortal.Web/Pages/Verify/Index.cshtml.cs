using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResultPortal.Web.Services;

namespace ResultPortal.Web.Pages.Verify;

/// <summary>
/// Result Portal page realizing UIS-03 (Architecture.md § 3.9): submits a VID to the Transparency &amp;
/// Audit Component's verify endpoint and renders the inclusion/settlement evidence or the reason
/// verification could not succeed (not found / election not finalized).
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

    [BindProperty(SupportsGet = true)]
    public string Vid { get; set; } = string.Empty;

    public VidVerificationResponse? Verification { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? InfoMessage { get; private set; }

    public string? CorrelationId { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ElectionId) || string.IsNullOrWhiteSpace(Vid))
        {
            InfoMessage = "Provide election id and VID to run verification.";
            return Page();
        }

        var accessDecision = _electionAccessPolicy.EvaluateVerificationAccess(ElectionId, User);
        if (!accessDecision.IsAllowed)
        {
            return ToActionResult(accessDecision);
        }

        try
        {
            var readResult = await _transparencyReadClient.VerifyByVidAsync(ElectionId, Vid, cancellationToken).ConfigureAwait(false);
            CorrelationId = readResult.CorrelationId;

            switch (readResult.Status)
            {
                case TransparencyReadStatus.Success:
                    Verification = readResult.Value;
                    break;
                case TransparencyReadStatus.NotFound:
                    ErrorMessage = "Verification response was not found for the provided election id and VID.";
                    break;
                case TransparencyReadStatus.Unauthorized:
                    ErrorMessage = "Result visibility policy denied this verification request.";
                    break;
                case TransparencyReadStatus.Unavailable:
                    ErrorMessage = "Transparency service is currently unavailable. Try again in a moment.";
                    break;
                default:
                    ErrorMessage = "Verification response could not be processed.";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify VID. ElectionId={ElectionId} Vid={Vid}", ElectionId, Vid);
            ErrorMessage = "Failed to verify VID.";
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
