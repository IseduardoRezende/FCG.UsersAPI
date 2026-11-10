using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Repositories;
using FCG.Users.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FCG.Users.Infrastructure.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly UsersDbContext _dbContext;

    public UserRoleRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserRole>()
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserRole>().AnyAsync(r => r.Id == id, cancellationToken);
    }
}
