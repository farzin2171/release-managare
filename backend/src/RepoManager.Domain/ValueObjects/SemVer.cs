namespace RepoManager.Domain.ValueObjects;

public sealed record SemVer(int Major, int Minor, int Patch)
{
    public static bool TryParse(string tag, out SemVer? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var s = tag.StartsWith('v') ? tag[1..] : tag;
        var parts = s.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;
        if (!int.TryParse(parts[2], out var patch)) return false;

        result = new SemVer(major, minor, patch);
        return true;
    }

    public SemVer NextMajor() => new(Major + 1, 0, 0);
    public SemVer NextMinor() => new(Major, Minor + 1, 0);
    public SemVer NextPatch() => new(Major, Minor, Patch + 1);

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
