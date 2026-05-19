namespace RepoManager.Domain.Enums;

public enum HealthBand
{
    Green,    // MatchRate >= 0.90
    Amber,    // MatchRate >= 0.60 && < 0.90
    Red,      // MatchRate < 0.60
    Unknown   // Comparison not supported (non-semver tag, etc.)
}
