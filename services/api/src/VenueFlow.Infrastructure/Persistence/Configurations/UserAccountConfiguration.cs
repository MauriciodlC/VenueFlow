using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Accounts;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public const int RoleMaxLength = 16;

    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("users", table =>
            table.HasCheckConstraint("ck_users_role", "role IN ('Guest', 'Host', 'Staff', 'Admin')"));

        builder.HasKey(user => user.Id).HasName("pk_users");
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(UserAccount.EmailMaxLength).IsRequired();
        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(UserAccount.DisplayNameMaxLength)
            .IsRequired();
        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(RoleMaxLength)
            .IsRequired();
        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(UserAccount.PasswordHashMaxLength)
            .IsRequired();

        builder.Ignore(user => user.IsHost);

        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("ux_users_email");
    }
}
