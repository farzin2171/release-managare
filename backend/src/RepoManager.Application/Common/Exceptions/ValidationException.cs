using FluentValidation.Results;

namespace RepoManager.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public IEnumerable<ValidationFailure> Failures { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures occurred.")
    {
        Failures = failures;
    }
}
