using Microsoft.AspNetCore.Http.HttpResults;
using WatcherAudit.Api.Domain;
using WatcherAudit.Api.Services;

namespace WatcherAudit.Api.Endpoints;

public static class WatcherEndpoints
{
    public static IEndpointRouteBuilder MapWatcherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/watcher").WithTags("Watcher");
        var protectedGroup = group.MapGroup(string.Empty).AddEndpointFilter<RequireOperatorAccessEndpointFilter>();

        group.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        protectedGroup.MapPost(
                "/elections/{electionId}/run-audit",
                async Task<Ok<WatcherReportResponse>>(
                    string electionId,
                    RunAuditRequest? request,
                    IWatcherAuditService watcherAuditService,
                    CancellationToken cancellationToken) =>
                {
                    var report = await watcherAuditService.RunAuditAsync(electionId, request?.RequestedBy, cancellationToken).ConfigureAwait(false);
                    return TypedResults.Ok(WatcherModelMapper.Map(report));
                })
            .WithName("RunWatcherAudit")
            .Produces<WatcherReportResponse>(StatusCodes.Status200OK);

        protectedGroup.MapGet(
                "/elections/{electionId}/latest-report",
                async Task<Results<Ok<WatcherReportResponse>, NotFound>>(
                    string electionId,
                    IWatcherReportRepository reportRepository,
                    CancellationToken cancellationToken) =>
                {
                    var report = await reportRepository.GetLatestByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    return report is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(WatcherModelMapper.Map(report));
                })
            .WithName("GetLatestWatcherReport")
            .Produces<WatcherReportResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapGet(
                "/elections/{electionId}/reports",
                async Task<IResult>(
                    string electionId,
                    IWatcherReportRepository reportRepository,
                    CancellationToken cancellationToken) =>
                {
                    var reports = await reportRepository.ListByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<WatcherReportResponse> mapped = reports.Select(WatcherModelMapper.Map).ToArray();
                    return TypedResults.Ok(mapped);
                })
            .WithName("ListWatcherReports")
            .Produces<IReadOnlyList<WatcherReportResponse>>(StatusCodes.Status200OK);

        protectedGroup.MapGet(
                "/reports/{reportId}",
                async Task<Results<Ok<WatcherReportResponse>, NotFound>>(
                    string reportId,
                    IWatcherReportRepository reportRepository,
                    CancellationToken cancellationToken) =>
                {
                    var report = await reportRepository.GetByIdAsync(reportId, cancellationToken).ConfigureAwait(false);
                    return report is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(WatcherModelMapper.Map(report));
                })
            .WithName("GetWatcherReportById")
            .Produces<WatcherReportResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapGet(
                "/reports/{reportId}/evidence",
                async Task<IResult>(
                    string reportId,
                    IWatcherReportRepository reportRepository,
                    CancellationToken cancellationToken) =>
                {
                    var report = await reportRepository.GetByIdAsync(reportId, cancellationToken).ConfigureAwait(false);
                    if (report is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(new
                    {
                        report.ReportId,
                        report.ElectionId,
                        report.GeneratedAtUtc,
                        report.ReportDigest,
                        report.Signature,
                        report.Evidence,
                        report.Checks
                    });
                })
            .WithName("ExportWatcherEvidence")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapGet(
                "/operator/elections/{electionId}/summary",
                async Task<Ok<OperatorElectionSummaryResponse>>(
                    string electionId,
                    IWatcherReportRepository reportRepository,
                    CancellationToken cancellationToken) =>
                {
                    var reports = await reportRepository.ListByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    var latest = reports.OrderByDescending(r => r.GeneratedAtUtc).FirstOrDefault();

                    return TypedResults.Ok(new OperatorElectionSummaryResponse(
                        electionId,
                        reports.Count,
                        reports.Count(r => r.Status == WatcherReportStatus.Consistent),
                        reports.Count(r => r.Status == WatcherReportStatus.Inconsistent),
                        reports.Count(r => r.Status == WatcherReportStatus.Error),
                        latest is null ? null : WatcherModelMapper.Map(latest)));
                })
            .WithName("GetOperatorElectionSummary")
            .Produces<OperatorElectionSummaryResponse>(StatusCodes.Status200OK);

        return app;
    }
}
