using Microsoft.AspNetCore.Http.HttpResults;
using ThresholdDecryption.Api.Services;

namespace ThresholdDecryption.Api.Endpoints;

public static class ThresholdEndpoints
{
    public static IEndpointRouteBuilder MapThresholdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/decryption").WithTags("ThresholdDecryption");
        var protectedGroup = group.MapGroup(string.Empty).AddEndpointFilter<RequireOperatorAccessEndpointFilter>();

        group.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        protectedGroup.MapPost(
                "/elections/{electionId}/ceremony",
                async Task<Ok<ThresholdCeremonyResponse>>(
                    string electionId,
                    ProposeCeremonyRequest request,
                    IThresholdDecryptionService service,
                    CancellationToken cancellationToken) =>
                {
                    var ceremony = await service.ProposeCeremonyAsync(
                            electionId,
                            request.RequiredShares,
                            request.RequiredApprovals,
                            request.Actor,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok(ThresholdMappers.Map(ceremony));
                })
            .WithName("ProposeThresholdCeremony")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK);

        protectedGroup.MapPost(
                "/elections/{electionId}/ceremony/approve",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string electionId,
                    ApproveCeremonyRequest request,
                    IThresholdDecryptionService service,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await service.ApproveCeremonyAsync(electionId, request.ApproverId, request.Note, cancellationToken).ConfigureAwait(false);
                    return updated is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(updated));
                })
            .WithName("ApproveThresholdCeremony")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapPost(
                "/elections/{electionId}/ceremony/share",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string electionId,
                    SubmitShareRequest request,
                    IThresholdDecryptionService service,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await service.SubmitShareAsync(electionId, request.TrusteeId, request.ShareDigest, cancellationToken).ConfigureAwait(false);
                    return updated is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(updated));
                })
            .WithName("SubmitThresholdShare")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapPost(
                "/elections/{electionId}/ceremony/finalize",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string electionId,
                    FinalizeDecryptionRequest request,
                    IThresholdDecryptionService service,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await service.FinalizeDecryptionAsync(electionId, request.Tallies, request.Actor, cancellationToken).ConfigureAwait(false);
                    return updated is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(updated));
                })
            .WithName("FinalizeThresholdDecryption")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapPost(
                "/elections/{electionId}/publish-final-tally",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string electionId,
                    PublishFinalTallyRequest request,
                    IThresholdDecryptionService service,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await service.PublishFinalTallyAsync(electionId, request.Actor, cancellationToken).ConfigureAwait(false);
                    return updated is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(updated));
                })
            .WithName("PublishThresholdFinalTally")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapGet(
                "/elections/{electionId}/ceremony",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string electionId,
                    IThresholdCeremonyRepository repository,
                    CancellationToken cancellationToken) =>
                {
                    var ceremony = await repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    return ceremony is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(ceremony));
                })
            .WithName("GetThresholdCeremonyByElection")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        protectedGroup.MapGet(
                "/ceremonies/{ceremonyId}",
                async Task<Results<Ok<ThresholdCeremonyResponse>, NotFound>>(
                    string ceremonyId,
                    IThresholdCeremonyRepository repository,
                    CancellationToken cancellationToken) =>
                {
                    var ceremony = await repository.GetByIdAsync(ceremonyId, cancellationToken).ConfigureAwait(false);
                    return ceremony is null ? TypedResults.NotFound() : TypedResults.Ok(ThresholdMappers.Map(ceremony));
                })
            .WithName("GetThresholdCeremonyById")
            .Produces<ThresholdCeremonyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
