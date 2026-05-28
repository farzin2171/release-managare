using System.Text.RegularExpressions;
using FluentValidation;
using RepoManager.Application.DTOs.Releases;

namespace RepoManager.Application.Validators;

public class PublishPagesRequestValidator : AbstractValidator<PublishPagesRequest>
{
    public PublishPagesRequestValidator()
    {
        RuleFor(x => x.Pages)
            .NotEmpty()
            .WithMessage("At least one page must be provided.")
            .WithErrorCode("pages_required");

        RuleForEach(x => x.Pages).ChildRules(page =>
        {
            page.RuleFor(p => p.Title)
                .NotEmpty()
                .MaximumLength(255)
                .WithErrorCode("invalid_title");

            page.RuleFor(p => p.BindingId)
                .NotEqual(Guid.Empty)
                .WithMessage("BindingId must be a valid non-empty GUID.")
                .WithErrorCode("invalid_binding_id");
        });
    }
}

public class ProjectCustomVariableUpsertValidator : AbstractValidator<(string Key, string Value)>
{
    private static readonly Regex KeyPattern = new(@"^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    public ProjectCustomVariableUpsertValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(KeyPattern)
            .WithMessage("Key must start with a letter and contain only letters, digits, and underscores.")
            .WithErrorCode("invalid_key");

        RuleFor(x => x.Value)
            .MaximumLength(500)
            .WithErrorCode("value_too_long");
    }
}
