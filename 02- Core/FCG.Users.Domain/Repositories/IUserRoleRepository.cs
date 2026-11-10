using FCG.Users.Domain.Entities;

namespace FCG.Users.Domain.Repositories;

public interface IUserRoleRepository
{
    Task<IReadOnlyList<UserRole>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
}
