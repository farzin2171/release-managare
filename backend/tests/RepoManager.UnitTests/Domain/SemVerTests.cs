using FluentAssertions;
using RepoManager.Domain.ValueObjects;

namespace RepoManager.UnitTests.Domain;

public class SemVerTests
{
    // TryParse — valid inputs

    [Theory]
    [InlineData("1.30.0", 1, 30, 0)]
    [InlineData("v2.5.7", 2, 5, 7)]
    [InlineData("0.9.0", 0, 9, 0)]
    public void TryParse_ValidTag_ReturnsTrueAndCorrectComponents(string tag, int major, int minor, int patch)
    {
        var result = SemVer.TryParse(tag, out var semver);

        result.Should().BeTrue();
        semver.Should().NotBeNull();
        semver!.Major.Should().Be(major);
        semver.Minor.Should().Be(minor);
        semver.Patch.Should().Be(patch);
    }

    // TryParse — invalid inputs

    [Theory]
    [InlineData("release-2026")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_InvalidTag_ReturnsFalseAndNullResult(string tag)
    {
        var result = SemVer.TryParse(tag, out var semver);

        result.Should().BeFalse();
        semver.Should().BeNull();
    }

    // ToString

    [Theory]
    [InlineData("1.30.0", "1.30.0")]
    [InlineData("v2.5.7", "2.5.7")]
    [InlineData("0.9.0", "0.9.0")]
    public void ToString_ReturnsNormalisedMajorMinorPatch(string tag, string expected)
    {
        SemVer.TryParse(tag, out var semver);

        semver!.ToString().Should().Be(expected);
    }

    // NextMinor

    [Theory]
    [InlineData("1.30.0", "1.31.0")]
    [InlineData("v2.5.7", "2.6.0")]
    [InlineData("0.9.0", "0.10.0")]
    public void NextMinor_IncrementsMinorAndResetsPath(string tag, string expectedNext)
    {
        SemVer.TryParse(tag, out var semver);

        semver!.NextMinor().ToString().Should().Be(expectedNext);
    }
}
