using Microsoft.Extensions.Options;
using WatcherAudit.Api.Application;

namespace WatcherAudit.Api.Endpoints;

public sealed class RequireOperatorAccessEndpointFilter(IOptions<WatcherOptions> options) : IEndpointFilter
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
