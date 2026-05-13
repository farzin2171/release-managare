namespace RepoManager.Application.Common.Exceptions;

public class ExternalServiceException : Exception
{
    public string Service { get; }

    public ExternalServiceException(string service, string message, Exception? inner = null)
        : base($"External service '{service}' error: {message}", inner)
    {
        Service = service;
    }
}
