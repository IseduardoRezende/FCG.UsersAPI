using FCG.Users.Application.Services;
using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Enums;
using FCG.Users.Domain.Repositories;
using Moq;

namespace FCG.Users.UnitTests.Services;

public class UserRoleServiceTests
{
    [Fact]
    public async Task GetAllAsync_WithSeededRoles_ReturnsUserAndAdministrator()
    {
        var repository = new Mock<IUserRoleRepository>();
        repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserRole { Id = (long)UserRoles.User, Name = nameof(UserRoles.User) },
            new UserRole { Id = (long)UserRoles.Administrator, Name = nameof(UserRoles.Administrator) }
        ]);

        var result = await new UserRoleService(repository.Object).GetAllAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Collection(result.Value!,
            role => Assert.Equal(nameof(UserRoles.User), role.Name),
            role => Assert.Equal(nameof(UserRoles.Administrator), role.Name));
    }
}
