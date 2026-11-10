using FCG.Contracts.Events;
using FCG.Users.Application.Abstractions;
using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Extensions;
using FCG.Users.Application.Services.Interfaces;
using FCG.Users.Domain.Commons;
using FCG.Users.Domain.Commons.Result;
using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Enums;
using FCG.Users.Domain.Filters;
using FCG.Users.Domain.Repositories;
using FCG.Users.Domain.Security;
using FluentValidation;

namespace FCG.Users.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<RegisterUserDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<UpdateUserDto> _updateValidator;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEventPublisher eventPublisher,
        ICurrentUser currentUser,
        IValidator<RegisterUserDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IValidator<UpdateUserDto> updateValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<ReadUserDto>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _registerValidator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<ReadUserDto>();

        if (validation is not null)
            return validation;

        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
            return ConflictResult<ReadUserDto>.Create(new Error("Email is already registered.", nameof(RegisterUserDto.Email)));

        var salt = _passwordHasher.GenerateSalt();

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            Salt = salt,
            Password = _passwordHasher.Hash(dto.Password, salt),
            UserRoleId = (long)UserRoles.User,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(new UserCreatedEvent(user.Id, user.Name, user.Email), cancellationToken);

        var created = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
        return SuccessResult<ReadUserDto>.Create(MapToReadDto(created!));
    }

    public async Task<Result<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _loginValidator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<TokenDto>();

        if (validation is not null)
            return validation;

        var user = await _userRepository.GetByEmailAsync(dto.Email.Trim(), cancellationToken);

        if (user is null || !_passwordHasher.Verify(user.Password, dto.Password, user.Salt))
            return NotFoundResult<TokenDto>.Create(new Error("Invalid email or password."));

        return SuccessResult<TokenDto>.Create(_tokenService.Generate(MapToReadDto(user)));
    }

    public async Task<Result<Pagination<ReadUserDto>>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.GetPagedAsync(filter, cancellationToken);
        var mapped = items.Select(MapToReadDto).ToList();

        return SuccessResult<Pagination<ReadUserDto>>.Create(
            new Pagination<ReadUserDto>(mapped, totalCount, filter.CurrentPage, filter.PageSize));
    }

    public async Task<Result<ReadUserDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAdministrator && _currentUser.UserId != id)
            return ForbiddenResult<ReadUserDto>.Create(new Error("You can only access your own user."));

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
            return NotFoundResult<ReadUserDto>.Create(new Error("User not found."));

        return SuccessResult<ReadUserDto>.Create(MapToReadDto(user));
    }

    public async Task<Result<ReadUserDto>> UpdateAsync(long id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _updateValidator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<ReadUserDto>();

        if (validation is not null)
            return validation;

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
            return NotFoundResult<ReadUserDto>.Create(new Error("User not found."));

        var email = dto.Email.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (existing is not null && existing.Id != id)
            return ConflictResult<ReadUserDto>.Create(new Error("Email is already registered.", nameof(UpdateUserDto.Email)));

        user.Name = dto.Name.Trim();
        user.Email = email;
        user.UserRoleId = dto.UserRoleId;
        user.UserRole = null;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var updated = await _userRepository.GetByIdAsync(id, cancellationToken);
        return SuccessResult<ReadUserDto>.Create(MapToReadDto(updated!));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
            return NotFoundResult<bool>.Create(new Error("User not found."));

        user.Delete();
        user.UserRole = null;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return SuccessResult<bool>.Create(true);
    }

    private static ReadUserDto MapToReadDto(User user)
    {
        return new ReadUserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            UserRoleId = user.UserRoleId,
            RoleName = user.UserRole?.Name ?? string.Empty,
            CreatedAt = user.CreatedAt
        };
    }
}
