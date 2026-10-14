using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ChainBallot.PublicContracts;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Pages;

/// <summary>
/// The Voter Component's ballot page (UIS-01/UIS-02, Architecture.md § 3.4): loads the voter's
/// dashboard state (VID, eligibility, candidates) on GET, and drives ballot encryption/submission via
/// <see cref="IVoterWorkflowService"/> on POST.
/// </summary>
public class DashboardModel(IVoterWorkflowService workflowService, IOptions<VoterOptions> options) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ElectionId { get; set; }

    [BindProperty]
    public BallotInputModel Input { get; set; } = new();

    public VoterDashboardState? Dashboard { get; private set; }

    public string? SubmissionFeedback { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var electionId = string.IsNullOrWhiteSpace(ElectionId)
            ? options.Value.DefaultElectionId
            : ElectionId.Trim();

        Dashboard = await workflowService.LoadDashboardAsync(electionId, cancellationToken);
        Input.ElectionId = Dashboard.ElectionId;
        Input.CandidateId = Dashboard.SelectedCandidateId ?? 0;
        Input.CandidateName = Dashboard.SelectedCandidateName ?? string.Empty;
    }

    public async Task<IActionResult> OnPostSubmitBallotAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await ReloadDashboardAsync(cancellationToken);
            return Page();
        }

        var result = await workflowService.SubmitBallotAsync(
            new BallotSelectionRequest(Input.ElectionId.Trim(), Input.CandidateId, Input.CandidateName.Trim()),
            cancellationToken);

        SubmissionFeedback = result.Message;
        await ReloadDashboardAsync(cancellationToken, result.QueueReceipt);

        return Page();
    }

    private async Task ReloadDashboardAsync(CancellationToken cancellationToken, QueueReceipt? lastReceipt = null)
    {
        var electionId = string.IsNullOrWhiteSpace(Input.ElectionId)
            ? options.Value.DefaultElectionId
            : Input.ElectionId.Trim();

        Dashboard = await workflowService.LoadDashboardAsync(electionId, cancellationToken);
        Dashboard = Dashboard with
        {
            LastQueueReceipt = lastReceipt
        };

        Input.ElectionId = Dashboard.ElectionId;
        Input.CandidateId = Input.CandidateId == 0
            ? Dashboard.SelectedCandidateId ?? 0
            : Input.CandidateId;
        Input.CandidateName = string.IsNullOrWhiteSpace(Input.CandidateName)
            ? Dashboard.SelectedCandidateName ?? string.Empty
            : Input.CandidateName;
    }

    public sealed class BallotInputModel
    {
        public string ElectionId { get; set; } = string.Empty;

        public int CandidateId { get; set; }

        public string CandidateName { get; set; } = string.Empty;
    }
}