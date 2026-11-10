using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Services;
using FCG.Users.Domain.Settings;
using Microsoft.Extensions.Options;

namespace FCG.Users.UnitTests.Services;

public class TokenServiceTests
{
    private static readonly TokenSettings Settings = new()
    {
        Issuer = "FCG.Users",
        Audience = "FCG.Client",
        Key = "unit-test-key-0123456789-abcdefghijklmnopqrstuvwxyz",
        DaysUntilExpires = 1
    };

    private static readonly ReadUserDto User = new()
    {
        Id = 7,
        Name = "Player",
        Email = "player@test.com",
        RoleName = "User"
    };

    private static JwtSecurityToken GenerateAndRead()
    {
        var token = new TokenService(Options.Create(Settings)).Generate(User);
        return new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
    }

    [Fact]
    public void Generate_ForUser_IncludesClaimsRequiredByOtherMicroservices()
    {
        var jwt = GenerateAndRead();

        Assert.Equal("7", jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "player@test.com");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Name && c.Value == "Player");
    }

    [Fact]
    public void Generate_ForUser_UsesConfiguredIssuerAndAudience()
    {
        var jwt = GenerateAndRead();

        Assert.Equal("FCG.Users", jwt.Issuer);
        Assert.Contains("FCG.Client", jwt.Audiences);
    }

    [Fact]
    public void Generate_ForUser_ExpiresAfterConfiguredDays()
    {
        var token = new TokenService(Options.Create(Settings)).Generate(User);

        Assert.InRange(token.ExpiresAt, DateTime.UtcNow.AddDays(1).AddMinutes(-1), DateTime.UtcNow.AddDays(1).AddMinutes(1));
    }
}
