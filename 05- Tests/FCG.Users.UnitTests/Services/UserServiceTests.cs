using FCG.Contracts.Events;
using FCG.Users.Application.Abstractions;
using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Services;
using FCG.Users.Application.Services.Interfaces;
using FCG.Users.Application.Validators;
using FCG.Users.Domain.Commons.Result;
using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Enums;
using FCG.Users.Domain.Repositories;
using FCG.Users.Domain.Security;
using Moq;

namespace FCG.Users.UnitTests.Services;

public class UserServiceTests
{
    private const string ValidPassword = "Abcdef1!";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserRoleRepository> _userRoleRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _passwordHasher.Setup(x => x.GenerateSalt()).Returns("salt");
        _passwordHasher.Setup(x => x.Hash(It.IsAny<string>(), It.IsAny<string>())).Returns("hashed");

        _tokenService.Setup(x => x.Generate(It.IsAny<ReadUserDto>()))
            .Returns(new TokenDto { Token = "token", ExpiresAt = DateTime.UtcNow.AddDays(1) });

        _userRoleRepository.Setup(x => x.ExistsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => id is (long)UserRoles.User or (long)UserRoles.Administrator);

        _userService = new UserService(
            _userRepository.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            _eventPublisher.Object,
            _currentUser.Object,
            new RegisterUserDtoValidator(),
            new LoginDtoValidator(),
            new UpdateUserDtoValidator(_userRoleRepository.Object));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static User CreateUser(long id = 1, string email = "user@test.com", UserRoles role = UserRoles.User) => new()
    {
        Id = id,
        Name = "User",
        Email = email,
        Password = "hashed",
        Salt = "salt",
        UserRoleId = (long)role,
        UserRole = new UserRole { Id = (long)role, Name = role.ToString() },
        CreatedAt = DateTime.UtcNow
    };

    private void SetupSuccessfulRegistration(Action<User>? onAdd = null)
    {
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userRepository.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) =>
            {
                user.Id = 42;
                onAdd?.Invoke(user);
            })
            .Returns(Task.CompletedTask);
        _userRepository.Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(42, "new@test.com"));
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_PersistsUserWithDefaultUserRole()
    {
        User? persisted = null;
        SetupSuccessfulRegistration(user => persisted = user);

        var result = await _userService.RegisterAsync(new RegisterUserDto { Name = "New", Email = "new@test.com", Password = ValidPassword }, Ct);

        Assert.IsType<SuccessResult<ReadUserDto>>(result);
        Assert.NotNull(persisted);
        Assert.Equal((long)UserRoles.User, persisted!.UserRoleId);
        _userRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_PublishesUserCreatedEventWithIdNameAndEmail()
    {
        SetupSuccessfulRegistration();

        await _userService.RegisterAsync(new RegisterUserDto { Name = "New", Email = "new@test.com", Password = ValidPassword }, Ct);

        _eventPublisher.Verify(x => x.PublishAsync(
            It.Is<UserCreatedEvent>(e => e.UserId == 42 && e.Name == "New" && e.Email == "new@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithMixedCaseEmailAndPaddedName_NormalizesBeforePersisting()
    {
        User? persisted = null;
        SetupSuccessfulRegistration(user => persisted = user);

        await _userService.RegisterAsync(new RegisterUserDto { Name = "  New  ", Email = "New@Test.COM", Password = ValidPassword }, Ct);

        Assert.Equal("New", persisted!.Name);
        Assert.Equal("new@test.com", persisted.Email);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_StoresHashedPasswordAndNeverPlainText()
    {
        User? persisted = null;
        SetupSuccessfulRegistration(user => persisted = user);

        await _userService.RegisterAsync(new RegisterUserDto { Name = "New", Email = "new@test.com", Password = ValidPassword }, Ct);

        Assert.Equal("hashed", persisted!.Password);
        Assert.Equal("salt", persisted.Salt);
        Assert.NotEqual(ValidPassword, persisted.Password);
    }

    [Fact]
    public async Task RegisterAsync_WithAlreadyRegisteredEmail_ReturnsConflictAndDoesNotPublishEvent()
    {
        _userRepository.Setup(x => x.ExistsByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _userService.RegisterAsync(new RegisterUserDto { Name = "User", Email = "user@test.com", Password = ValidPassword }, Ct);

        Assert.IsType<ConflictResult<ReadUserDto>>(result);
        _userRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisher.Verify(x => x.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ReturnsInvalidAndDoesNotPersistNorPublish()
    {
        var result = await _userService.RegisterAsync(new RegisterUserDto { Name = "User", Email = "user@test.com", Password = "weak" }, Ct);

        Assert.IsType<InvalidResult<ReadUserDto>>(result);
        _userRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisher.Verify(x => x.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        _userRepository.Setup(x => x.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser());
        _passwordHasher.Setup(x => x.Verify("hashed", ValidPassword, "salt")).Returns(true);

        var result = await _userService.LoginAsync(new LoginDto { Email = "user@test.com", Password = ValidPassword }, Ct);

        Assert.IsType<SuccessResult<TokenDto>>(result);
        Assert.Equal("token", result.Value!.Token);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNotFoundWithoutGeneratingToken()
    {
        _userRepository.Setup(x => x.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser());
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await _userService.LoginAsync(new LoginDto { Email = "user@test.com", Password = "Wrong123!" }, Ct);

        Assert.IsType<NotFoundResult<TokenDto>>(result);
        _tokenService.Verify(x => x.Generate(It.IsAny<ReadUserDto>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsNotFound()
    {
        _userRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _userService.LoginAsync(new LoginDto { Email = "missing@test.com", Password = ValidPassword }, Ct);

        Assert.IsType<NotFoundResult<TokenDto>>(result);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmailFormat_ReturnsInvalid()
    {
        var result = await _userService.LoginAsync(new LoginDto { Email = "not-an-email", Password = ValidPassword }, Ct);

        Assert.IsType<InvalidResult<TokenDto>>(result);
        _userRepository.Verify(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_AsAdministrator_ReturnsAnyUser()
    {
        _currentUser.Setup(x => x.IsAdministrator).Returns(true);
        _currentUser.Setup(x => x.UserId).Returns(99);
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));

        var result = await _userService.GetByIdAsync(1, Ct);

        Assert.IsType<SuccessResult<ReadUserDto>>(result);
        Assert.Equal(1, result.Value!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_AsTheUserHimself_ReturnsOwnData()
    {
        _currentUser.Setup(x => x.UserId).Returns(1);
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));

        var result = await _userService.GetByIdAsync(1, Ct);

        Assert.IsType<SuccessResult<ReadUserDto>>(result);
    }

    [Fact]
    public async Task GetByIdAsync_AsAnotherNonAdminUser_ReturnsForbiddenWithoutQueryingRepository()
    {
        _currentUser.Setup(x => x.UserId).Returns(2);

        var result = await _userService.GetByIdAsync(1, Ct);

        Assert.IsType<ForbiddenResult<ReadUserDto>>(result);
        _userRepository.Verify(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _currentUser.Setup(x => x.IsAdministrator).Returns(true);
        _userRepository.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _userService.GetByIdAsync(1, Ct);

        Assert.IsType<NotFoundResult<ReadUserDto>>(result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesNameEmailAndRole()
    {
        User? updated = null;
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));
        _userRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => updated = user)
            .Returns(Task.CompletedTask);

        var result = await _userService.UpdateAsync(1, new UpdateUserDto
        {
            Name = "Renamed",
            Email = "Renamed@Test.com",
            UserRoleId = (long)UserRoles.Administrator
        }, Ct);

        Assert.IsType<SuccessResult<ReadUserDto>>(result);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("renamed@test.com", updated.Email);
        Assert.Equal((long)UserRoles.Administrator, updated.UserRoleId);
    }

    [Fact]
    public async Task UpdateAsync_WithEmailOwnedByAnotherUser_ReturnsConflict()
    {
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));
        _userRepository.Setup(x => x.GetByEmailAsync("other@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(2, "other@test.com"));

        var result = await _userService.UpdateAsync(1, new UpdateUserDto
        {
            Name = "User",
            Email = "other@test.com",
            UserRoleId = (long)UserRoles.User
        }, Ct);

        Assert.IsType<ConflictResult<ReadUserDto>>(result);
        _userRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_KeepingOwnEmail_DoesNotReturnConflict()
    {
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));
        _userRepository.Setup(x => x.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));

        var result = await _userService.UpdateAsync(1, new UpdateUserDto
        {
            Name = "User",
            Email = "user@test.com",
            UserRoleId = (long)UserRoles.User
        }, Ct);

        Assert.IsType<SuccessResult<ReadUserDto>>(result);
    }

    [Fact]
    public async Task UpdateAsync_WithNonexistentRole_ReturnsInvalid()
    {
        var result = await _userService.UpdateAsync(1, new UpdateUserDto
        {
            Name = "User",
            Email = "user@test.com",
            UserRoleId = 999
        }, Ct);

        Assert.IsType<InvalidResult<ReadUserDto>>(result);
    }

    [Fact]
    public async Task UpdateAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _userService.UpdateAsync(1, new UpdateUserDto
        {
            Name = "User",
            Email = "user@test.com",
            UserRoleId = (long)UserRoles.User
        }, Ct);

        Assert.IsType<NotFoundResult<ReadUserDto>>(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_MarksUserAsDeletedInsteadOfRemoving()
    {
        User? updated = null;
        _userRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(1));
        _userRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => updated = user)
            .Returns(Task.CompletedTask);

        var result = await _userService.DeleteAsync(1, Ct);

        Assert.IsType<SuccessResult<bool>>(result);
        Assert.True(updated!.IsDeleted);
        _userRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _userService.DeleteAsync(1, Ct);

        Assert.IsType<NotFoundResult<bool>>(result);
    }
}
