using FCG.Users.Infrastructure.Security;

namespace FCG.Users.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_WithSamePasswordAndSalt_ReturnsTrue()
    {
        var salt = _hasher.GenerateSalt();
        var hash = _hasher.Hash("Abcdef1!", salt);

        Assert.True(_hasher.Verify(hash, "Abcdef1!", salt));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var salt = _hasher.GenerateSalt();
        var hash = _hasher.Hash("Abcdef1!", salt);

        Assert.False(_hasher.Verify(hash, "Wrong123!", salt));
    }

    [Fact]
    public void GenerateSalt_CalledTwice_ProducesDifferentSalts()
    {
        Assert.NotEqual(_hasher.GenerateSalt(), _hasher.GenerateSalt());
    }

    [Fact]
    public void Verify_WithDocumentedDefaultAdminPassword_MatchesSeededHash()
    {
        Assert.True(_hasher.Verify(
            "OxFPEaZRtmloJcAIHMItyJfep3S4tc5/ViQaZxtiiDQ=",
            "Fcg@Admin2026!",
            "7f3c9a2e1b4d6f8a0c2e4a6b8d0f2a4c"));
    }
}
