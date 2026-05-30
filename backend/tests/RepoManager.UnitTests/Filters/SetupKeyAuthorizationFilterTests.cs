using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Moq;
using RepoManager.Api.Filters;

namespace RepoManager.UnitTests.Filters;

public class SetupKeyAuthorizationFilterTests
{
    private const string ValidKey = "super-secret-setup-key-minimum-32-chars";

    private static SetupKeyAuthorizationFilter CreateFilter(string? configuredKey = ValidKey)
    {
        var dict = configuredKey is not null
            ? new Dictionary<string, string?> { ["RELEASE_MANAGER_SETUP_KEY"] = configuredKey }
            : new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new SetupKeyAuthorizationFilter(config);
    }

    private static (ActionExecutingContext Context, Mock<ActionExecutionDelegate> Next) BuildContext(
        string? headerValue = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
            httpContext.Request.Headers["X-Setup-Key"] = headerValue;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var args = new Dictionary<string, object?>();
        var context = new ActionExecutingContext(actionContext, filters, args, controller: new object());

        var executedContext = new ActionExecutedContext(actionContext, filters, controller: new object());
        var next = new Mock<ActionExecutionDelegate>();
        next.Setup(n => n()).ReturnsAsync(executedContext);

        return (context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingHeader_Returns401WithSetupKeyInvalidCode()
    {
        var filter = CreateFilter();
        var (ctx, next) = BuildContext(headerValue: null);

        await filter.OnActionExecutionAsync(ctx, next.Object);

        var result = ctx.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(401);
        result.Value.Should().BeEquivalentTo(new { code = "setup_key_invalid" });
        next.Verify(n => n(), Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WrongKey_Returns401WithSetupKeyInvalidCode()
    {
        var filter = CreateFilter();
        var (ctx, next) = BuildContext(headerValue: "definitely-wrong-key-value");

        await filter.OnActionExecutionAsync(ctx, next.Object);

        var result = ctx.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(401);
        result.Value.Should().BeEquivalentTo(new { code = "setup_key_invalid" });
        next.Verify(n => n(), Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_CorrectKey_CallsNextDelegate()
    {
        var filter = CreateFilter();
        var (ctx, next) = BuildContext(headerValue: ValidKey);

        await filter.OnActionExecutionAsync(ctx, next.Object);

        ctx.Result.Should().BeNull("the filter must not short-circuit when the key is correct");
        next.Verify(n => n(), Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_EmptyHeader_Returns401()
    {
        var filter = CreateFilter();
        var (ctx, next) = BuildContext(headerValue: "");

        await filter.OnActionExecutionAsync(ctx, next.Object);

        ctx.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(401);
        next.Verify(n => n(), Times.Never);
    }
}
