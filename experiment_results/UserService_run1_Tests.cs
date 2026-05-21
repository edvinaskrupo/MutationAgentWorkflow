using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using MutationAgentWorkflow.Sample;
using Xunit;

namespace MutationAgentWorkflow.Tests;

public class UserServiceTests
{
    private sealed class TestUserRepository : IUserRepository
    {
        public bool ExistsByEmailResult { get; set; }
        public User? FindByEmailResult { get; set; }
        public List<User> GetAllResult { get; set; } = new();
        public User? SavedUser { get; private set; }
        public int SaveCallCount { get; private set; }

        public bool ExistsByEmail(string email) => ExistsByEmailResult;

        public void Save(User user)
        {
            SaveCallCount++;
            SavedUser = user;
        }

        public User? FindByEmail(string email) => FindByEmailResult;

        public List<User> GetAll() => GetAllResult;
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string HashResult { get; set; } = "hashed";
        public bool VerifyResult { get; set; }
        public string? LastHashInput { get; private set; }
        public string? LastVerifyPlainPassword { get; private set; }
        public string? LastVerifyHashedPassword { get; private set; }

        public string Hash(string plainPassword)
        {
            LastHashInput = plainPassword;
            return HashResult;
        }

        public bool Verify(string plainPassword, string hashedPassword)
        {
            LastVerifyPlainPassword = plainPassword;
            LastVerifyHashedPassword = hashedPassword;
            return VerifyResult;
        }
    }

    [Theory]
    [InlineData(null, "Name", "Password1")]
    [InlineData("", "Name", "Password1")]
    [InlineData("   ", "Name", "Password1")]
    [InlineData("email@test.com", null, "Password1")]
    [InlineData("email@test.com", "", "Password1")]
    [InlineData("email@test.com", "   ", "Password1")]
    public void Register_InvalidEmailOrName_ReturnsFalse(string? email, string? name, string plainPassword)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Register(email!, name!, plainPassword);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void Register_InvalidPasswordLength_ReturnsFalse(string? plainPassword)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Register("email@test.com", "Name", plainPassword!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Register_DuplicateEmail_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository { ExistsByEmailResult = true };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Register("email@test.com", "Name", "Password1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Register_ValidInput_SavesNormalizedUserAndReturnsTrue()
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher { HashResult = "hashed-password" };
        var service = new UserService(repository, hasher);
        var before = DateTime.UtcNow;

        // Act
        var result = service.Register("  EMAIL@TEST.COM  ", "  Alice  ", "Password1");
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(result);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.NotNull(repository.SavedUser);
        Assert.Equal("email@test.com", repository.SavedUser!.Email);
        Assert.Equal("Alice", repository.SavedUser.Name);
        Assert.Equal("hashed-password", repository.SavedUser.PasswordHash);
        Assert.True(repository.SavedUser.IsActive);
        Assert.True(repository.SavedUser.CreatedAt >= before && repository.SavedUser.CreatedAt <= after);
        Assert.Equal("Password1", hasher.LastHashInput);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Authenticate_InvalidEmailOrPassword_ReturnsFalse(string? input)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Authenticate(input!, "Password1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Authenticate_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository { FindByEmailResult = null };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Authenticate("  EMAIL@TEST.COM  ", "Password1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Authenticate_InactiveUser_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            FindByEmailResult = new User
            {
                Email = "email@test.com",
                PasswordHash = "hashed",
                IsActive = false
            }
        };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Authenticate("email@test.com", "Password1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Authenticate_ValidActiveUserAndPasswordVerificationSucceeds_ReturnsTrue()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            FindByEmailResult = new User
            {
                Email = "email@test.com",
                PasswordHash = "hashed",
                IsActive = true
            }
        };
        var hasher = new TestPasswordHasher { VerifyResult = true };
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Authenticate("  EMAIL@TEST.COM  ", "Password1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Authenticate_ValidActiveUserAndPasswordVerificationFails_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            FindByEmailResult = new User
            {
                Email = "email@test.com",
                PasswordHash = "hashed",
                IsActive = true
            }
        };
        var hasher = new TestPasswordHasher { VerifyResult = false };
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Authenticate("email@test.com", "Password1");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deactivate_InvalidEmail_ReturnsFalse(string? email)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Deactivate(email!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Deactivate_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository { FindByEmailResult = null };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Deactivate("email@test.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Deactivate_ActiveUser_UpdatesUserAndSaves()
    {
        // Arrange
        var user = new User
        {
            Email = "email@test.com",
            IsActive = true
        };
        var repository = new TestUserRepository { FindByEmailResult = user };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.Deactivate("  EMAIL@TEST.COM  ");

        // Assert
        Assert.True(result);
        Assert.False(user.IsActive);
        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public void GetActiveUserCount_EmptyCollection_ReturnsZero()
    {
        // Arrange
        var repository = new TestUserRepository { GetAllResult = new List<User>() };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.GetActiveUserCount();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetActiveUserCount_MixedUsers_ReturnsOnlyActiveCount()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            GetAllResult = new List<User>
            {
                new() { Email = "a@test.com", IsActive = true },
                new() { Email = "b@test.com", IsActive = false },
                new() { Email = "c@test.com", IsActive = true }
            }
        };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.GetActiveUserCount();

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetActiveUserEmails_EmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        var repository = new TestUserRepository { GetAllResult = new List<User>() };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.GetActiveUserEmails();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveUserEmails_MixedUsers_ReturnsSortedActiveEmails()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            GetAllResult = new List<User>
            {
                new() { Email = "z@test.com", IsActive = true },
                new() { Email = "b@test.com", IsActive = false },
                new() { Email = "a@test.com", IsActive = true }
            }
        };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.GetActiveUserEmails();

        // Assert
        Assert.Equal(new[] { "a@test.com", "z@test.com" }, result);
    }

    [Theory]
    [InlineData(null, "OldPassword1", "NewPassword1")]
    [InlineData("", "OldPassword1", "NewPassword1")]
    [InlineData("   ", "OldPassword1", "NewPassword1")]
    public void ChangePassword_InvalidEmail_ReturnsFalse(string? email, string oldPassword, string newPassword)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword(email!, oldPassword, newPassword);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void ChangePassword_InvalidNewPasswordLength_ReturnsFalse(string? newPassword)
    {
        // Arrange
        var repository = new TestUserRepository();
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword("email@test.com", "OldPassword1", newPassword!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository { FindByEmailResult = null };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword("email@test.com", "OldPassword1", "NewPassword1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_InactiveUser_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            FindByEmailResult = new User
            {
                Email = "email@test.com",
                PasswordHash = "old-hash",
                IsActive = false
            }
        };
        var hasher = new TestPasswordHasher();
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword("email@test.com", "OldPassword1", "NewPassword1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_ReturnsFalse()
    {
        // Arrange
        var repository = new TestUserRepository
        {
            FindByEmailResult = new User
            {
                Email = "email@test.com",
                PasswordHash = "old-hash",
                IsActive = true
            }
        };
        var hasher = new TestPasswordHasher { VerifyResult = false };
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword("  EMAIL@TEST.COM  ", "OldPassword1", "NewPassword1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_ValidRequest_UpdatesPasswordAndSaves()
    {
        // Arrange
        var user = new User
        {
            Email = "email@test.com",
            PasswordHash = "old-hash",
            IsActive = true
        };
        var repository = new TestUserRepository { FindByEmailResult = user };
        var hasher = new TestPasswordHasher
        {
            VerifyResult = true,
            HashResult = "new-hash"
        };
        var service = new UserService(repository, hasher);

        // Act
        var result = service.ChangePassword("  EMAIL@TEST.COM  ", "OldPassword1", "NewPassword1");

        // Assert
        Assert.True(result);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(1, repository.SaveCallCount);
    }
}

namespace MutationAgentWorkflow.Sample.Tests;

public class UserServiceIntegrationTests
{
    [Theory]
    [InlineData(null, "John Doe", "password123")]
    [InlineData("", "John Doe", "password123")]
    [InlineData("   ", "John Doe", "password123")]
    [InlineData("john@example.com", null, "password123")]
    [InlineData("john@example.com", "", "password123")]
    [InlineData("john@example.com", "   ", "password123")]
    [InlineData("john@example.com", "John Doe", null)]
    [InlineData("john@example.com", "John Doe", "short")]
    public void Register_InvalidInput_ExpectedBehavior(string? email, string? name, string? plainPassword)
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Register(email!, name!, plainPassword!);

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Register_DuplicateEmail_ExpectedBehavior()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        repositoryMock.Setup(r => r.ExistsByEmail("john@example.com")).Returns(true);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Register(" john@example.com ", "  John Doe  ", "password123");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.ExistsByEmail("john@example.com"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Register_ValidInput_SavesNormalizedUserAndReturnsTrue()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.ExistsByEmail("john@example.com")).Returns(false);
        hasherMock.Setup(h => h.Hash("password123")).Returns("hashed-password");

        User? savedUser = null;
        repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
            .Callback<User>(u => savedUser = u);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Register(" John@Example.com ", "  John Doe  ", "password123");

        // Assert
        Assert.True(result);
        Assert.NotNull(savedUser);
        Assert.Equal("john@example.com", savedUser!.Email);
        Assert.Equal("John Doe", savedUser.Name);
        Assert.Equal("hashed-password", savedUser.PasswordHash);
        Assert.Equal(DateTimeKind.Utc, savedUser.CreatedAt.Kind);
        Assert.True(savedUser.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
        Assert.True(savedUser.CreatedAt >= DateTime.UtcNow.AddMinutes(-1));
        Assert.True(savedUser.IsActive);

        repositoryMock.Verify(r => r.ExistsByEmail("john@example.com"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
            u.Email == "john@example.com" &&
            u.Name == "John Doe" &&
            u.PasswordHash == "hashed-password" &&
            u.IsActive &&
            u.CreatedAt.Kind == DateTimeKind.Utc)), Times.Once);
        hasherMock.Verify(h => h.Hash("password123"), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "password123")]
    [InlineData("", "password123")]
    [InlineData("   ", "password123")]
    [InlineData("john@example.com", null)]
    [InlineData("john@example.com", "")]
    [InlineData("john@example.com", "   ")]
    public void Authenticate_InvalidInput_ExpectedBehavior(string? email, string? plainPassword)
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Authenticate(email!, plainPassword!);

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_UserNotFound_ReturnsFalseAndDoesNotVerifyPassword()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns((User?)null);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Authenticate(" John@Example.com ", "password123");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void Authenticate_InactiveUser_ReturnsFalseAndDoesNotVerifyPassword()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com"))
            .Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = false
            });

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Authenticate(" John@Example.com ", "password123");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void Authenticate_ValidActiveUserAndPassword_ReturnsTrue()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com"))
            .Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = true
            });

        hasherMock.Setup(h => h.Verify("password123", "hashed-password")).Returns(true);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Authenticate(" John@Example.com ", "password123");

        // Assert
        Assert.True(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify("password123", "hashed-password"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_ValidActiveUserButPasswordVerificationFails_ReturnsFalse()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com"))
            .Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = true
            });

        hasherMock.Setup(h => h.Verify("wrong-password", "hashed-password")).Returns(false);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Authenticate("John@Example.com", "wrong-password");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify("wrong-password", "hashed-password"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deactivate_InvalidEmail_ExpectedBehavior(string? email)
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Deactivate(email!);

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Deactivate_UserNotFound_ReturnsFalseAndDoesNotSave()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns((User?)null);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Deactivate(" John@Example.com ");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Deactivate_ActiveUser_SetsInactiveAndSavesUpdatedUser()
    {
        // Arrange
        var user = new User
        {
            Email = "john@example.com",
            Name = "John Doe",
            PasswordHash = "hashed-password",
            IsActive = true
        };

        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(user);
        repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
            .Callback<User>(u => user = u);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.Deactivate(" John@Example.com ");

        // Assert
        Assert.True(result);
        Assert.False(user.IsActive);

        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
            u.Email == "john@example.com" &&
            u.Name == "John Doe" &&
            u.PasswordHash == "hashed-password" &&
            u.IsActive == false)), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetActiveUserCount_MixedUsers_ReturnsOnlyActiveCount()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>
        {
            new User { Email = "a@example.com", IsActive = true },
            new User { Email = "b@example.com", IsActive = false },
            new User { Email = "c@example.com", IsActive = true }
        });

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.GetActiveUserCount();

        // Assert
        Assert.Equal(2, result);
        repositoryMock.Verify(r => r.GetAll(), Times.Once);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetActiveUserCount_EmptyList_ReturnsZero()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.GetActiveUserCount();

        // Assert
        Assert.Equal(0, result);
        repositoryMock.Verify(r => r.GetAll(), Times.Once);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetActiveUserEmails_MixedUsers_ReturnsSortedActiveEmails()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>
        {
            new User { Email = "zeta@example.com", IsActive = true },
            new User { Email = "beta@example.com", IsActive = false },
            new User { Email = "alpha@example.com", IsActive = true },
            new User { Email = "gamma@example.com", IsActive = true }
        });

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.GetActiveUserEmails();

        // Assert
        Assert.Equal(new List<string>
        {
            "alpha@example.com",
            "gamma@example.com",
            "zeta@example.com"
        }, result);

        repositoryMock.Verify(r => r.GetAll(), Times.Once);
        repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_InvalidEmailOrNewPassword_ExpectedBehavior()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result1 = service.ChangePassword(null!, "oldPassword", "newPassword123");
        var result2 = service.ChangePassword("", "oldPassword", "newPassword123");
        var result3 = service.ChangePassword("   ", "oldPassword", "newPassword123");
        var result4 = service.ChangePassword("john@example.com", "oldPassword", null!);
        var result5 = service.ChangePassword("john@example.com", "oldPassword", "short");

        // Assert
        Assert.False(result1);
        Assert.False(result2);
        Assert.False(result3);
        Assert.False(result4);
        Assert.False(result5);
        repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_UserNotFound_ReturnsFalseAndDoesNotVerifyOrSave()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns((User?)null);
        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.ChangePassword(" John@Example.com ", "oldPassword", "newPassword123");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_InactiveUser_ReturnsFalseAndDoesNotUpdatePassword()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        repositoryMock.Setup(r => r.FindByEmail("john@example.com"))
            .Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "old-hash",
                IsActive = false
            });

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.ChangePassword(" John@Example.com ", "oldPassword", "newPassword123");

        // Assert
        Assert.False(result);
        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_ReturnsFalseAndDoesNotSave()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        var user = new User
        {
            Email = "john@example.com",
            Name = "John Doe",
            PasswordHash = "old-hash",
            IsActive = true
        };

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(user);
        hasherMock.Setup(h => h.Verify("wrong-old-password", "old-hash")).Returns(false);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.ChangePassword(" John@Example.com ", "wrong-old-password", "newPassword123");

        // Assert
        Assert.False(result);
        Assert.Equal("old-hash", user.PasswordHash);

        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify("wrong-old-password", "old-hash"), Times.Once);
        hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_ValidCredentials_UpdatesPasswordAndSavesUser()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

        var user = new User
        {
            Email = "john@example.com",
            Name = "John Doe",
            PasswordHash = "old-hash",
            IsActive = true
        };

        repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(user);
        hasherMock.Setup(h => h.Verify("oldPassword123", "old-hash")).Returns(true);
        hasherMock.Setup(h => h.Hash("newPassword123")).Returns("new-hash");

        repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
            .Callback<User>(u => user = u);

        var service = new UserService(repositoryMock.Object, hasherMock.Object);

        // Act
        var result = service.ChangePassword(" John@Example.com ", "oldPassword123", "newPassword123");

        // Assert
        Assert.True(result);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal("john@example.com", user.Email);

        repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
        hasherMock.Verify(h => h.Verify("oldPassword123", "old-hash"), Times.Once);
        hasherMock.Verify(h => h.Hash("newPassword123"), Times.Once);
        repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
            u.Email == "john@example.com" &&
            u.PasswordHash == "new-hash" &&
            u.IsActive)), Times.Once);
        repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.GetAll(), Times.Never);
    }
}