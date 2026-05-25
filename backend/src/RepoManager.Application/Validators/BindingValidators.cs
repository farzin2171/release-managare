using FluentValidation;
using RepoManager.Application.DTOs.Bindings;

namespace RepoManager.Application.Validators;

public class CreateBindingRequestValidator : AbstractValidator<CreateBindingRequest>
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.Ordinal) { "ReleaseNotes", "Checklist", "Custom" };

    public CreateBindingRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("TemplateId must be a valid non-empty GUID.")
            .WithErrorCode("invalid_template_id");

        RuleFor(x => x.Kind)
            .NotEmpty()
            .Must(k => ValidKinds.Contains(k))
            .WithMessage("Kind must be one of: ReleaseNotes, Checklist, Custom.")
            .WithErrorCode("invalid_kind");

        RuleFor(x => x.PageTitleTemplate)
            .NotEmpty()
            .MaximumLength(500)
            .WithErrorCode("invalid_page_title_template");

        RuleFor(x => x.ParentPageId)
            .MaximumLength(100)
            .When(x => x.ParentPageId is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("invalid_sort_order");
    }
}

public class UpdateBindingRequestValidator : AbstractValidator<UpdateBindingRequest>
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.Ordinal) { "ReleaseNotes", "Checklist", "Custom" };

    public UpdateBindingRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .When(x => x.TemplateId.HasValue)
            .WithErrorCode("invalid_template_id");

        RuleFor(x => x.Kind)
            .Must(k => ValidKinds.Contains(k!))
            .When(x => x.Kind is not null)
            .WithMessage("Kind must be one of: ReleaseNotes, Checklist, Custom.")
            .WithErrorCode("invalid_kind");

        RuleFor(x => x.PageTitleTemplate)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => x.PageTitleTemplate is not null)
            .WithErrorCode("invalid_page_title_template");

        RuleFor(x => x.ParentPageId)
            .MaximumLength(100)
            .When(x => x.ParentPageId is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SortOrder.HasValue)
            .WithErrorCode("invalid_sort_order");
    }
}
