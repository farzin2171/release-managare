using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Common.Exceptions;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, extensions) = exception switch
        {
            NotFoundException e => (404, "Not Found", (object?)null),
            ConflictException e => (409, "Conflict", (object?)null),
            ValidationException e => (400, "Validation Failed", (object?)new { errors = e.Failures.GroupBy(f => f.PropertyName).ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()) }),
            ExternalServiceException e => (502, "External Service Error", (object?)null),
            _ => (500, "An unexpected error occurred", (object?)null)
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var traceId = context.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        if (extensions is not null)
            problem.Extensions["errors"] = ((dynamic)extensions).errors;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
