using FCG.Users.Application.DTOs.Users;
using FCG.Users.Domain.Commons.Result;

namespace FCG.Users.Application.Services.Interfaces;

public interface IUserRoleService
{
    Task<Result<IReadOnlyList<ReadUserRoleDto>>> GetAllAsync(CancellationToken cancellationToken = default);
}
