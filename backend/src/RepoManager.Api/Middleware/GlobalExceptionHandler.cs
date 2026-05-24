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
        var (status, title) = exception switch
        {
            NotFoundException => (404, "Not Found"),
            ConflictException => (409, "Conflict"),
            ValidationException => (400, "Validation Failed"),
            ExternalServiceException => (502, "External Service Error"),
            _ => (500, "An unexpected error occurred")
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        switch (exception)
        {
            case ConflictException { Code: not null } ce:
                problem.Extensions["code"] = ce.Code;
                break;
            case ValidationException ve:
                problem.Extensions["errors"] = ve.Failures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
                break;
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
