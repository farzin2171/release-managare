using FluentValidation;

namespace RepoManager.Application.Repositories;

public record SetLatestTagDto(string TagName);

public class SetLatestTagDtoValidator : AbstractValidator<SetLatestTagDto>
{
    public SetLatestTagDtoValidator()
    {
        RuleFor(x => x.TagName)
            .NotEmpty()
            .MaximumLength(250);
    }
}
