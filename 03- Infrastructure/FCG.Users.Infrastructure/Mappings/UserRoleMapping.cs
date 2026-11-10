using FCG.Users.Domain.Entities;
using FCG.Users.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Users.Infrastructure.Mappings;

public class UserRoleMapping : BaseEntityMapping<UserRole>
{
    public override void Configure(EntityTypeBuilder<UserRole> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Name);

        builder.HasData(
            new UserRole { Id = (long)UserRoles.User, Name = nameof(UserRoles.User), IsDeleted = false },
            new UserRole { Id = (long)UserRoles.Administrator, Name = nameof(UserRoles.Administrator), IsDeleted = false }
        );
    }
}
