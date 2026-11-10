using FCG.Users.Application.DTOs.Users;
using FCG.Users.Domain.Commons;
using FCG.Users.Domain.Commons.Result;
using FCG.Users.Domain.Filters;

namespace FCG.Users.Application.Services.Interfaces;

public interface IUserService
{
    Task<Result<ReadUserDto>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);

    Task<Result<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);

    Task<Result<Pagination<ReadUserDto>>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default);

    Task<Result<ReadUserDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<ReadUserDto>> UpdateAsync(long id, UpdateUserDto dto, CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
