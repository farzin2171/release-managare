namespace RepoManager.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public string? Code { get; }

    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, string code) : base(message)
    {
        Code = code;
    }
}
