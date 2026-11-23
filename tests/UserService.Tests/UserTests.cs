using UserService.Domain.Entities;

namespace UserService.Tests;

public class UserTests
{
    [Fact]
    public void CreateNormalizesIdentityAndAssignsDefaultRole()
    {
        var user = User.Create("  PERSON@EXAMPLE.COM ", "hash", " Jane ", " Doe ");

        Assert.Equal("person@example.com", user.Email);
        Assert.Equal("Jane Doe", user.FullName);
        Assert.Equal(Role.UserId, user.RoleId);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void CreateRejectsMissingEmail() =>
        Assert.Throws<ArgumentException>(() => User.Create(" ", "hash", "Jane", "Doe"));

    [Fact]
    public void DeactivateRejectsRepeatedTransition()
    {
        var user = User.Create("person@example.com", "hash", "Jane", "Doe");
        user.Deactivate();
        Assert.Throws<InvalidOperationException>(user.Deactivate);
        user.Activate();
        Assert.Equal(UserStatus.Active, user.Status);
    }
}
