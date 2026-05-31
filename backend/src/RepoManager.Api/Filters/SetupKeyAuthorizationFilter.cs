using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace RepoManager.Api.Filters;

public sealed class SetupKeyAuthorizationFilter : IAsyncActionFilter
{
    private readonly IConfiguration _configuration;

    public SetupKeyAuthorizationFilter(IConfiguration configuration)
        => _configuration = configuration;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuredKey = _configuration["RELEASE_MANAGER_SETUP_KEY"] ?? string.Empty;
        var headerValue = context.HttpContext.Request.Headers["X-Setup-Key"].ToString();

        if (!ConstantTimeEquals(headerValue, configuredKey))
        {
            context.Result = new ObjectResult(new { code = "setup_key_invalid" }) { StatusCode = 401 };
            return;
        }

        await next();
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
