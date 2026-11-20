using VenueFlow.Domain;
using VenueFlow.Domain.Accounts;

namespace VenueFlow.Tests.Domain;

public sealed class UserAccountTests
{
    private static readonly Guid UserId = Guid.Parse("8c2d4e6f-1a3b-4c5d-8e9f-0a1b2c3d4e01");

    [Fact]
    public void Create_normalizes_the_email_and_trims_the_name()
    {
        var user = UserAccount.Create(UserId, "  Host@Example.TEST ", " Demo host ", UserRole.Host);

        Assert.Equal("host@example.test", user.Email);
        Assert.Equal("Demo host", user.DisplayName);
        Assert.Equal(UserRole.Host, user.Role);
        Assert.True(user.IsHost);
        Assert.Equal(string.Empty, user.PasswordHash);
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@example.test")]
    [InlineData("host@")]
    [InlineData("a@b@example.test")]
    [InlineData("host name@example.test")]
    public void Create_rejects_malformed_emails(string email)
    {
        var error = Assert.Throws<DomainException>(() => UserAccount.Create(UserId, email, "Demo", UserRole.Guest));

        Assert.Equal("email_invalid", error.Code);
    }

    [Fact]
    public void Create_rejects_unknown_roles_and_empty_ids()
    {
        Assert.Equal("user_role_invalid",
            Assert.Throws<DomainException>(() => UserAccount.Create(UserId, "a@example.test", "A", (UserRole)42)).Code);
        Assert.Equal("user_id_required",
            Assert.Throws<DomainException>(() => UserAccount.Create(Guid.Empty, "a@example.test", "A", UserRole.Guest)).Code);
    }

    [Fact]
    public void SetPasswordHash_rejects_blank_values()
    {
        var user = UserAccount.Create(UserId, "a@example.test", "A", UserRole.Guest);

        Assert.Equal("password_hash_invalid",
            Assert.Throws<DomainException>(() => user.SetPasswordHash(" ")).Code);
    }
}
