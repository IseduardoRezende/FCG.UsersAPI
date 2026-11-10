using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Services.Interfaces;
using FCG.Users.Domain.Commons.Result;
using FCG.Users.Domain.Repositories;

namespace FCG.Users.Application.Services;

public class UserRoleService : IUserRoleService
{
    private readonly IUserRoleRepository _userRoleRepository;

    public UserRoleService(IUserRoleRepository userRoleRepository)
    {
        _userRoleRepository = userRoleRepository;
    }

    public async Task<Result<IReadOnlyList<ReadUserRoleDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _userRoleRepository.GetAllAsync(cancellationToken);

        var result = roles.Select(r => new ReadUserRoleDto
        {
            Id = r.Id,
            Name = r.Name
        }).ToList();

        return SuccessResult<IReadOnlyList<ReadUserRoleDto>>.Create(result);
    }
}
