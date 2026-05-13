namespace RepoManager.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string resourceType, object id)
        : base($"{resourceType} with id '{id}' was not found.")
    {
    }
}
