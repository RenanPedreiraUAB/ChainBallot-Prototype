using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class ProverReadinessService(
    IOptions<ProcessingOptions> options,
    IHttpClientFactory httpClientFactory) : IProverReadinessService
{
    public async Task<ProverReadinessSnapshot> GetReadinessAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        var mode = config.ProverMode;

        if (string.Equals(mode, ProverModes.DeterministicStub, StringComparison.OrdinalIgnoreCase))
        {
            return new ProverReadinessSnapshot(mode, true, null, DateTimeOffset.UtcNow);
        }

        if (string.Equals(mode, ProverModes.LocalProcess, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.LocalProcessProver.ExecutablePath))
            {
                return new ProverReadinessSnapshot(mode, false, "ExecutablePath is not configured.", DateTimeOffset.UtcNow);
            }

            if (!File.Exists(config.LocalProcessProver.ExecutablePath))
            {
                return new ProverReadinessSnapshot(mode, false, "Configured local prover executable was not found.", DateTimeOffset.UtcNow);
            }

            return new ProverReadinessSnapshot(mode, true, null, DateTimeOffset.UtcNow);
        }

        if (string.Equals(mode, ProverModes.ExternalHttp, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.ExternalProver.Endpoint))
            {
                return new ProverReadinessSnapshot(mode, false, "External prover endpoint is not configured.", DateTimeOffset.UtcNow);
            }

            try
            {
                var client = httpClientFactory.CreateClient(nameof(ProverReadinessService));
                using var request = new HttpRequestMessage(HttpMethod.Head, config.ExternalProver.Endpoint);
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new ProverReadinessSnapshot(mode, false, $"External prover endpoint returned {(int)response.StatusCode}.", DateTimeOffset.UtcNow);
                }

                return new ProverReadinessSnapshot(mode, true, null, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                return new ProverReadinessSnapshot(mode, false, ex.Message, DateTimeOffset.UtcNow);
            }
        }

        return new ProverReadinessSnapshot(mode, false, "Unsupported prover mode configured.", DateTimeOffset.UtcNow);
    }
}
