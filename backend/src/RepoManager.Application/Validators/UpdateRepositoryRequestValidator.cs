using FluentValidation;
using RepoManager.Application.Repositories;

namespace RepoManager.Application.Validators;

public class UpdateRepositoryRequestValidator : AbstractValidator<UpdateRepositoryRequest>
{
    public UpdateRepositoryRequestValidator()
    {
        RuleFor(x => x.ServiceOwner)
            .MaximumLength(120)
            .When(x => x.ServiceOwner is not null);
    }
}
