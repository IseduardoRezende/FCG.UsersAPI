using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Filters;
using FCG.Users.Domain.Repositories;
using FCG.Users.Infrastructure.DbContexts;
using FCG.Users.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FCG.Users.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _dbContext;

    public UserRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<User>()
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        return await _dbContext.Set<User>()
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        return await _dbContext.Set<User>()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<User>()
            .Include(u => u.UserRole)
            .Where(ApplyFilter(filter));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplyOrdering(filter)
            .ApplyPagination(filter)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<User>().AddAsync(user, cancellationToken);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<User>().Update(user);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private static Expression<Func<User, bool>> ApplyFilter(UserFilter filter)
    {
        var value = filter.Value?.ToLower();

        return u =>
            (filter.UserRoleId == null || u.UserRoleId == filter.UserRoleId) &&
            (string.IsNullOrWhiteSpace(value) ||
             u.Name.ToLower().Contains(value) ||
             u.Email.ToLower().Contains(value));
    }
}
