using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FCG.Contracts.Events;
using FCG.Users.IntegrationTests.Infrastructure;
using FCG.Users.Infrastructure.DbContexts;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FCG.Users.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace FCG.Users.IntegrationTests;

public class UsersEndpointsTests
{
    private readonly UsersApiFactory _factory;

    public UsersEndpointsTests(UsersApiFactory factory)
    {
        _factory = factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_WithValidData_Returns201AndPersistsUserInDatabase()
    {
        var email = NewEmail();

        var response = await _factory.CreateClient().RegisterAsync(email, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await dbContext.Set<FCG.Users.Domain.Entities.User>().SingleAsync(u => u.Email == email, Ct);
        Assert.Equal((long)FCG.Users.Domain.Enums.UserRoles.User, user.UserRoleId);
        Assert.NotEqual(ValidPassword, user.Password);
    }

    [Fact]
    public async Task Register_WithValidData_PublishesUserCreatedEventToTheBroker()
    {
        var email = NewEmail();

        await _factory.CreateClient().RegisterAsync(email, Ct);

        var harness = _factory.Services.GetTestHarness();
        Assert.True(await harness.Published.Any<UserCreatedEvent>(m => m.Context.Message.Email == email, Ct));
    }

    [Fact]
    public async Task Register_WithAlreadyRegisteredEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await client.RegisterAsync(email, Ct);

        var response = await client.RegisterAsync(email.ToUpperInvariant(), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns422()
    {
        var response = await _factory.CreateClient().RegisterAsync(NewEmail(), Ct, password: "weak");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdministrator_ReturnsJwtWithAdministratorRoleAndEmail()
    {
        var token = await _factory.CreateClient().LoginAsync(AdminEmail, AdminPassword, Ct);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Administrator");
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == AdminEmail);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns404()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/users/logins", new { email = AdminEmail, password = "Wrong123!" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/users", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_WithCommonUserToken_Returns403()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await client.RegisterAsync(email, Ct);
        client.WithToken(await client.LoginAsync(email, ValidPassword, Ct));

        var response = await client.GetAsync("/api/v1/users", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_WithAdministratorToken_Returns200()
    {
        var client = _factory.CreateClient();
        client.WithToken(await client.LoginAsync(AdminEmail, AdminPassword, Ct));

        var response = await client.GetAsync("/api/v1/users", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_ForAnotherUserWithCommonToken_Returns403()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await client.RegisterAsync(email, Ct);
        client.WithToken(await client.LoginAsync(email, ValidPassword, Ct));

        var response = await client.GetAsync("/api/v1/users/-1", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_ForOwnUser_Returns200WithOwnData()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        var registered = await (await client.RegisterAsync(email, Ct)).ReadValueAsync(Ct);
        client.WithToken(await client.LoginAsync(email, ValidPassword, Ct));

        var response = await client.GetAsync($"/api/v1/users/{registered.GetProperty("id").GetInt64()}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await response.ReadValueAsync(Ct)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task DeleteUser_AsAdministrator_SoftDeletesAndBlocksFutureLogin()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        var registered = await (await client.RegisterAsync(email, Ct)).ReadValueAsync(Ct);
        client.WithToken(await client.LoginAsync(AdminEmail, AdminPassword, Ct));

        var response = await client.DeleteAsync($"/api/v1/users/{registered.GetProperty("id").GetInt64()}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/users/logins", new { email, password = ValidPassword }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
    }

    [Fact]
    public async Task Health_WhenDatabaseIsReachable_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
