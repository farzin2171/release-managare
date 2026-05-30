using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace RepoManager.Api.Filters;

public sealed class SetupKeyAuthorizationFilter : IAsyncActionFilter
{
    public SetupKeyAuthorizationFilter(IConfiguration configuration) { }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        => next();
}
