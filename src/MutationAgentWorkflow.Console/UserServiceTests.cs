using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace MutationAgentWorkflow.Sample.Tests;

public class UserServiceIntegrationTests
{
    [Theory]
    [InlineData(null, "Alice", "password123", false)]
    [InlineData("", "Alice", "password123", false)]
    [InlineData("   ", "Alice", "password123", false)]
    [InlineData("alice@example.com", null, "password123", false)]
    [InlineData("alice@example.com", "", "password123", false)]
    [InlineData("alice@example.com", "   ", "password123", false)]
    public void Register_InvalidEmailOrNameInputs_ReturnsFalseAndDoesNotSave(string? email, string? name, string plainPassword, bool expected)
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        // Act
        var result = sut.Register(email ?? string.Empty, name ?? string.Empty, plainPassword);

        // Assert
        Assert.Equal(expected, result);
        repo.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "Alice", false)]
    [InlineData("", "Alice", false)]
    [InlineData("short7", "Alice", false)]
    [InlineData("1234567", "Alice", false)]
    [InlineData("        ", "Alice", false)]
    [InlineData("password8", "Alice", true)]
    public void Register_PlainPasswordLengthBoundary_SucceedsOnlyWhenLengthAtLeast8(string? plainPassword, string name, bool expected)
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var email = "alice@example.com";

        if (expected)
        {
            repo.Setup(r => r.ExistsByEmail(email)).Returns(false);
            hasher.Setup(h => h.Hash(plainPassword!)).Returns("hashed");
            repo.Setup(r => r.Save(It.Is<User>(u =>
                u.Email == email &&
                u.Name == name.Trim() &&
                u.PasswordHash == "hashed" &&
                u.IsActive == true &&
                u.CreatedAt.Kind == DateTimeKind.Utc
            )));
        }

        // Act
        var result = sut.Register(email, name, plainPassword!);

        // Assert
        Assert.Equal(expected, result);

        if (!expected)
        {
            repo.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        }
        else
        {
            repo.Verify(r => r.ExistsByEmail(email), Times.Once);
            hasher.Verify(h => h.Hash(plainPassword!), Times.Once);
            repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
        }
    }

    [Fact]
    public void Register_WhenRepositorySaysEmailExists_ReturnsFalseAndDoesNotHashOrSave()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.ExistsByEmail("alice@example.com")).Returns(true);

        // Act
        var result = sut.Register("alice@example.com", "Alice", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.ExistsByEmail("alice@example.com"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Register_EmailNormalizationTrimAndLowercase_SavesNormalizedUserAndReturnsTrue()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var inputEmail = "  Alice@Example.com  ";
        var expectedEmail = "alice@example.com";
        repo.Setup(r => r.ExistsByEmail(inputEmail)).Returns(false);
        hasher.Setup(h => h.Hash("password123")).Returns("hash123");

        repo.Setup(r => r.Save(It.Is<User>(u =>
            u.Email == expectedEmail &&
            u.Name == "Alice" &&
            u.PasswordHash == "hash123" &&
            u.IsActive == true &&
            u.CreatedAt.Kind == DateTimeKind.Utc
        )));

        // Act
        var result = sut.Register(inputEmail, " Alice ", "password123");

        // Assert
        Assert.True(result);
        repo.Verify(r => r.ExistsByEmail(inputEmail), Times.Once);
        hasher.Verify(h => h.Hash("password123"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public void Authenticate_InvalidEmailOrPasswordInputs_ReturnsFalseAndDoesNotLookupOrVerify()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        // Act
        var r1 = sut.Authenticate(null!, "password");
        var r2 = sut.Authenticate("   ", "password");
        var r3 = sut.Authenticate("alice@example.com", null!);
        var r4 = sut.Authenticate("alice@example.com", "   ");

        // Assert
        Assert.False(r1);
        Assert.False(r2);
        Assert.False(r3);
        Assert.False(r4);

        repo.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_WhenRepositoryReturnsNull_ReturnsFalseAndDoesNotVerify()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns((User?)null);

        // Act
        var result = sut.Authenticate(" Alice@Example.com ", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_WhenUserIsInactive_ReturnsFalseAndDoesNotVerify()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = false
        });

        // Act
        var result = sut.Authenticate("  ALICE@Example.com ", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_WhenPasswordVerificationFails_ReturnsFalse()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "storedHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        hasher.Setup(h => h.Verify("wrongPassword", "storedHash")).Returns(false);

        // Act
        var result = sut.Authenticate("alice@example.com", "wrongPassword");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify("wrongPassword", "storedHash"), Times.Once);
    }

    [Fact]
    public void Authenticate_HappyPath_ActiveUserAndValidPassword_ReturnsTrue()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "storedHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        hasher.Setup(h => h.Verify("password123", "storedHash")).Returns(true);

        // Act
        var result = sut.Authenticate("  ALICE@Example.com  ", "password123");

        // Assert
        Assert.True(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify("password123", "storedHash"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deactivate_InvalidEmailInputs_ReturnsFalseAndDoesNotLookupOrSave(string? email)
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        // Act
        var result = sut.Deactivate(email ?? string.Empty);

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void Deactivate_WhenRepositoryReturnsNull_ReturnsFalseAndDoesNotSave()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns((User?)null);

        // Act
        var result = sut.Deactivate(" Alice@Example.com ");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void Deactivate_WhenUserIsFound_SetsIsActiveFalseAndSaves()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var user = new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(user);
        repo.Setup(r => r.Save(It.Is<User>(u => ReferenceEquals(u, user) && u.IsActive == false)));

        // Act
        var result = sut.Deactivate("  ALICE@Example.com ");

        // Assert
        Assert.True(result);
        Assert.False(user.IsActive);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Deactivate_WhenUserAlreadyInactive_ReturnsTrueAndStillSavesWithIsActiveFalse()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var user = new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = false
        };

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(user);
        repo.Setup(r => r.Save(It.Is<User>(u => ReferenceEquals(u, user) && u.IsActive == false)));

        // Act
        var result = sut.Deactivate("alice@example.com");

        // Assert
        Assert.True(result);
        Assert.False(user.IsActive);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public void GetActiveUserCount_RepositoryEmpty_Returns0()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.GetAll()).Returns(new List<User>());

        // Act
        var count = sut.GetActiveUserCount();

        // Assert
        Assert.Equal(0, count);
        repo.Verify(r => r.GetAll(), Times.Once);
    }

    [Fact]
    public void GetActiveUserCount_MixtureOfActiveAndInactive_ReturnsOnlyActiveCount()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.GetAll()).Returns(new List<User>
        {
            new User { Email = "a@x.com", Name = "A", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = true },
            new User { Email = "b@x.com", Name = "B", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = false },
            new User { Email = "c@x.com", Name = "C", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = true }
        });

        // Act
        var count = sut.GetActiveUserCount();

        // Assert
        Assert.Equal(2, count);
        repo.Verify(r => r.GetAll(), Times.Once);
    }

    [Fact]
    public void GetActiveUserEmails_EmptyRepository_ReturnsEmptyList()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.GetAll()).Returns(new List<User>());

        // Act
        var emails = sut.GetActiveUserEmails();

        // Assert
        Assert.NotNull(emails);
        Assert.Empty(emails);
        repo.Verify(r => r.GetAll(), Times.Once);
    }

    [Fact]
    public void GetActiveUserEmails_OnlyActiveIncluded_OrderedAscending_UsesStoredEmailCasing()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.GetAll()).Returns(new List<User>
        {
            new User { Email = "z@x.com", Name = "Z", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = true },
            new User { Email = "m@x.com", Name = "M", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = false },
            new User { Email = "B@x.com", Name = "B", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = true },
            new User { Email = "a@x.com", Name = "A", PasswordHash = "h", CreatedAt = DateTime.UtcNow, IsActive = true }
        });

        // Act
        var emails = sut.GetActiveUserEmails();

        // Assert
        var expected = new[] { "B@x.com", "a@x.com", "z@x.com" }.ToList(); // OrderBy uses string ascending comparer
        Assert.Equal(expected, emails);
        repo.Verify(r => r.GetAll(), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_InvalidEmailInputs_ReturnsFalseAndDoesNotLookupOrHash(string? email)
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        // Act
        var result = sut.ChangePassword(email ?? string.Empty, "oldPassword", "newPassword123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("short7", false)]
    [InlineData("1234567", false)]
    [InlineData("        ", false)]
    [InlineData("password123", true)]
    public void ChangePassword_NewPasswordLengthBoundary_ReturnsExpectedAndDoesNotChange_WhenInvalid(string? newPassword, bool expected)
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var user = new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "oldHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        if (expected)
        {
            repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(user);
            hasher.Setup(h => h.Verify("oldPassword", "oldHash")).Returns(true);
            hasher.Setup(h => h.Hash("password123")).Returns("newHash");
            repo.Setup(r => r.Save(It.Is<User>(u => ReferenceEquals(u, user) && u.PasswordHash == "newHash")));
        }

        // Act
        var result = sut.ChangePassword("alice@example.com", "oldPassword", newPassword!);

        // Assert
        Assert.Equal(expected, result);

        if (!expected)
        {
            repo.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        }
        else
        {
            repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
            hasher.Verify(h => h.Verify("oldPassword", "oldHash"), Times.Once);
            hasher.Verify(h => h.Hash("password123"), Times.Once);
            repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
            Assert.Equal("newHash", user.PasswordHash);
        }
    }

    [Fact]
    public void ChangePassword_WhenRepositoryReturnsNull_ReturnsFalseAndDoesNotVerifyOrSave()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns((User?)null);

        // Act
        var result = sut.ChangePassword("  ALICE@Example.com  ", "oldPassword", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_WhenUserIsInactive_ReturnsFalseAndDoesNotVerifyOrSave()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "oldHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = false
        });

        // Act
        var result = sut.ChangePassword("alice@example.com", "oldPassword", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_WhenOldPasswordVerificationFails_ReturnsFalseAndDoesNotHashNewOrSave()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var user = new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "oldHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(user);
        hasher.Setup(h => h.Verify("wrongOldPassword", "oldHash")).Returns(false);

        // Act
        var result = sut.ChangePassword("  ALICE@Example.com  ", "wrongOldPassword", "password123");

        // Assert
        Assert.False(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify("wrongOldPassword", "oldHash"), Times.Once);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_HappyPath_UpdatesPasswordHashAndSaves_ReturnsTrue()
    {
        // Arrange
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = new UserService(repo.Object, hasher.Object);

        var user = new User
        {
            Email = "alice@example.com",
            Name = "Alice",
            PasswordHash = "oldHash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        repo.Setup(r => r.FindByEmail("alice@example.com")).Returns(user);
        hasher.Setup(h => h.Verify("oldPassword", "oldHash")).Returns(true);
        hasher.Setup(h => h.Hash("newPassword123")).Returns("newHash");
        repo.Setup(r => r.Save(It.Is<User>(u => ReferenceEquals(u, user) && u.PasswordHash == "newHash")));

        // Act
        var result = sut.ChangePassword("  ALICE@Example.com  ", "oldPassword", "newPassword123");

        // Assert
        Assert.True(result);
        repo.Verify(r => r.FindByEmail("alice@example.com"), Times.Once);
        hasher.Verify(h => h.Verify("oldPassword", "oldHash"), Times.Once);
        hasher.Verify(h => h.Hash("newPassword123"), Times.Once);
        repo.Verify(r => r.Save(It.IsAny<User>()), Times.Once);
        Assert.Equal("newHash", user.PasswordHash);
    }
}