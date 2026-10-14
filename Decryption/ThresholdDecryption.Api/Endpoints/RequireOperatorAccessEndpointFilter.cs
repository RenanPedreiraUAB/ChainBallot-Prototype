using Microsoft.Extensions.Options;
using ThresholdDecryption.Api.Application;

namespace ThresholdDecryption.Api.Endpoints;

public sealed class RequireOperatorAccessEndpointFilter(IOptions<ThresholdOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var settings = options.Value;

        if (context.HttpContext.Request.Headers.TryGetValue(settings.OperatorAccessHeaderName, out var provided)
            && string.Equals(provided.ToString(), settings.OperatorAccessKey, StringComparison.Ordinal))
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(Results.Unauthorized());
    }
}
