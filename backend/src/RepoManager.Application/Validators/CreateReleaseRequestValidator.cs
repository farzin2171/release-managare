using FluentValidation;
using RepoManager.Application.Releases;

namespace RepoManager.Application.Validators;

public class CreateReleaseRequestValidator : AbstractValidator<CreateReleaseRequest>
{
    private static readonly HashSet<string> ValidBumpTypes =
        new(StringComparer.OrdinalIgnoreCase) { "major", "minor", "patch", "manual" };

    public CreateReleaseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Repositories)
            .NotEmpty()
            .WithMessage("At least one repository must be selected.")
            .WithErrorCode("at_least_one_repo_required");

        RuleForEach(x => x.Repositories).ChildRules(repo =>
        {
            repo.RuleFor(r => r.NextVersion)
                .Must(BeValidSemver)
                .WithMessage("'{PropertyValue}' is not a valid semver string.")
                .WithErrorCode("invalid_semver");

            repo.RuleFor(r => r.BumpType)
                .Must(t => ValidBumpTypes.Contains(t))
                .WithMessage("BumpType must be one of: major, minor, patch, manual.")
                .WithErrorCode("invalid_bump_type");
        });
    }

    private static bool BeValidSemver(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        var parts = version.Split('.');
        return parts.Length == 3
            && parts.All(p => int.TryParse(p, out var n) && n >= 0);
    }
}
