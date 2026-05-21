using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using MutationAgentWorkflow.Sample;
using Xunit;

namespace MutationAgentWorkflow.Sample.Tests
{
    public class UserServiceTests
    {
        private sealed class TestUserRepository : IUserRepository
        {
            public Dictionary<string, User> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<User> SavedUsers { get; } = new();

            public bool ExistsByEmail(string email) => Users.ContainsKey(email);

            public void Save(User user)
            {
                Users[user.Email] = user;
                SavedUsers.Add(user);
            }

            public User? FindByEmail(string email)
            {
                Users.TryGetValue(email, out var user);
                return user;
            }

            public List<User> GetAll() => Users.Values.ToList();
        }

        private sealed class TestPasswordHasher : IPasswordHasher
        {
            public Func<string, string> HashFunc { get; set; } = p => $"HASH:{p}";
            public Func<string, string, bool> VerifyFunc { get; set; } = (p, h) => h == $"HASH:{p}";

            public string Hash(string plainPassword) => HashFunc(plainPassword);

            public bool Verify(string plainPassword, string hashedPassword) => VerifyFunc(plainPassword, hashedPassword);
        }

        [Theory]
        [InlineData(null, "Name", "Password1")]
        [InlineData("", "Name", "Password1")]
        [InlineData("   ", "Name", "Password1")]
        [InlineData("email@example.com", null, "Password1")]
        [InlineData("email@example.com", "", "Password1")]
        [InlineData("email@example.com", "   ", "Password1")]
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
        [InlineData("short7")]
        [InlineData("1234567")]
        public void Register_InvalidPassword_ReturnsFalse(string? plainPassword)
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("email@example.com", "Name", plainPassword!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Register_DuplicateEmail_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["email@example.com"] = new User { Email = "email@example.com", IsActive = true };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("email@example.com", "Name", "Password1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Register_ValidInput_SavesNormalizedUserAndReturnsTrue()
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);
            var email = "  Test@Example.Com  ";
            var name = "  Alice  ";
            var password = "Password1";

            // Act
            var result = service.Register(email, name, password);

            // Assert
            Assert.True(result);
            Assert.Single(repository.SavedUsers);
            Assert.Equal("test@example.com", repository.SavedUsers[0].Email);
            Assert.Equal("Alice", repository.SavedUsers[0].Name);
            Assert.Equal("HASH:Password1", repository.SavedUsers[0].PasswordHash);
            Assert.True(repository.SavedUsers[0].IsActive);
        }

        [Theory]
        [InlineData(null, "Password1")]
        [InlineData("", "Password1")]
        [InlineData("   ", "Password1")]
        [InlineData("email@example.com", null)]
        [InlineData("email@example.com", "")]
        [InlineData("email@example.com", "   ")]
        public void Authenticate_InvalidEmailOrPassword_ReturnsFalse(string? email, string? plainPassword)
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate(email!, plainPassword!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate("  unknown@example.com  ", "Password1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_InactiveUser_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["user@example.com"] = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:Password1",
                IsActive = false
            };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate(" user@example.com ", "Password1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_WrongPassword_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["user@example.com"] = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:Password1",
                IsActive = true
            };
            var hasher = new TestPasswordHasher();
            hasher.VerifyFunc = (_, _) => false;
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate(" user@example.com ", "WrongPassword");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_CorrectPassword_ReturnsTrue()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["user@example.com"] = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:Password1",
                IsActive = true
            };
            var hasher = new TestPasswordHasher();
            hasher.VerifyFunc = (plainPassword, hashedPassword) => plainPassword == "Password1" && hashedPassword == "HASH:Password1";
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate(" user@example.com ", "Password1");

            // Assert
            Assert.True(result);
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
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate(" user@example.com ");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Deactivate_ExistingUser_SetsInactiveAndSaves()
        {
            // Arrange
            var repository = new TestUserRepository();
            var user = new User
            {
                Email = "user@example.com",
                IsActive = true
            };
            repository.Users[user.Email] = user;
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate(" user@example.com ");

            // Assert
            Assert.True(result);
            Assert.False(user.IsActive);
            Assert.Single(repository.SavedUsers);
        }

        [Fact]
        public void Deactivate_AlreadyInactiveUser_StillSetsInactiveAndSaves()
        {
            // Arrange
            var repository = new TestUserRepository();
            var user = new User
            {
                Email = "user@example.com",
                IsActive = false
            };
            repository.Users[user.Email] = user;
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate("user@example.com");

            // Assert
            Assert.True(result);
            Assert.False(user.IsActive);
            Assert.Single(repository.SavedUsers);
        }

        [Fact]
        public void GetActiveUserCount_EmptyRepository_ReturnsZero()
        {
            // Arrange
            var repository = new TestUserRepository();
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
            var repository = new TestUserRepository();
            repository.Users["a@example.com"] = new User { Email = "a@example.com", IsActive = true };
            repository.Users["b@example.com"] = new User { Email = "b@example.com", IsActive = false };
            repository.Users["c@example.com"] = new User { Email = "c@example.com", IsActive = true };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserCount();

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void GetActiveUserEmails_EmptyRepository_ReturnsEmptyList()
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetActiveUserEmails_AllInactiveUsers_ReturnsEmptyList()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["b@example.com"] = new User { Email = "b@example.com", IsActive = false };
            repository.Users["a@example.com"] = new User { Email = "a@example.com", IsActive = false };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetActiveUserEmails_MixedUsers_ReturnsOnlyActiveEmailsOrderedAscending()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["c@example.com"] = new User { Email = "c@example.com", IsActive = true };
            repository.Users["a@example.com"] = new User { Email = "a@example.com", IsActive = true };
            repository.Users["b@example.com"] = new User { Email = "b@example.com", IsActive = false };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("a@example.com", result[0]);
            Assert.Equal("c@example.com", result[1]);
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
        [InlineData("short7")]
        [InlineData("1234567")]
        public void ChangePassword_InvalidNewPassword_ReturnsFalse(string? newPassword)
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("user@example.com", "OldPassword1", newPassword!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ChangePassword_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword(" user@example.com ", "OldPassword1", "NewPassword1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ChangePassword_InactiveUser_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            repository.Users["user@example.com"] = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:OldPassword1",
                IsActive = false
            };
            var hasher = new TestPasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword(" user@example.com ", "OldPassword1", "NewPassword1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ChangePassword_IncorrectOldPassword_ReturnsFalse()
        {
            // Arrange
            var repository = new TestUserRepository();
            var user = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:OldPassword1",
                IsActive = true
            };
            repository.Users[user.Email] = user;
            var hasher = new TestPasswordHasher();
            hasher.VerifyFunc = (_, _) => false;
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword(" user@example.com ", "WrongOldPassword", "NewPassword1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ChangePassword_CorrectOldPassword_UpdatesPasswordAndSaves()
        {
            // Arrange
            var repository = new TestUserRepository();
            var user = new User
            {
                Email = "user@example.com",
                PasswordHash = "HASH:OldPassword1",
                IsActive = true
            };
            repository.Users[user.Email] = user;
            var hasher = new TestPasswordHasher();
            hasher.HashFunc = p => $"NEW_HASH:{p}";
            hasher.VerifyFunc = (plainPassword, hashedPassword) => plainPassword == "OldPassword1" && hashedPassword == "HASH:OldPassword1";
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword(" user@example.com ", "OldPassword1", "NewPassword1");

            // Assert
            Assert.True(result);
            Assert.Equal("NEW_HASH:NewPassword1", user.PasswordHash);
            Assert.Single(repository.SavedUsers);
        }

        [Theory]
        [InlineData(null, "John Doe", "password123")]
        [InlineData("", "John Doe", "password123")]
        [InlineData("   ", "John Doe", "password123")]
        [InlineData("john@example.com", null, "password123")]
        [InlineData("john@example.com", "", "password123")]
        [InlineData("john@example.com", "   ", "password123")]
        [InlineData("john@example.com", "John Doe", null)]
        [InlineData("john@example.com", "John Doe", "short")]
        public void Register_InvalidInput_ReturnsFalseAndDoesNotCallDependencies(string? email, string? name, string? plainPassword)
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Register_DuplicateEmail_ReturnsFalseAndDoesNotSaveOrHash()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            repositoryMock.Setup(r => r.ExistsByEmail("john@example.com")).Returns(true);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Register("john@example.com", "John Doe", "password123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.ExistsByEmail("john@example.com"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Register_ValidInput_MapsAndPersistsNormalizedUser_ReturnsTrue()
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
            var result = service.Register("  John@Example.Com  ", "  John Doe  ", "password123");

            // Assert
            Assert.True(result);

            repositoryMock.Verify(r => r.ExistsByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Hash("password123"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john@example.com" &&
                u.Name == "John Doe" &&
                u.PasswordHash == "hashed-password" &&
                u.IsActive &&
                u.CreatedAt != default &&
                u.CreatedAt <= DateTime.UtcNow)), Times.Once);

            Assert.NotNull(savedUser);
            Assert.Equal("john@example.com", savedUser!.Email);
            Assert.Equal("John Doe", savedUser.Name);
            Assert.Equal("hashed-password", savedUser.PasswordHash);
            Assert.True(savedUser.IsActive);
            Assert.NotEqual(default, savedUser.CreatedAt);
            Assert.True(savedUser.CreatedAt <= DateTime.UtcNow);

            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null, "password123")]
        [InlineData("", "password123")]
        [InlineData("   ", "password123")]
        [InlineData("john@example.com", null)]
        [InlineData("john@example.com", "")]
        [InlineData("john@example.com", "   ")]
        public void Authenticate_InvalidInput_ReturnsFalseAndDoesNotCallDependencies(string? email, string? plainPassword)
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_UserNotFound_ReturnsFalseAndVerifiesLookupNormalization()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns((User?)null);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Authenticate("  John@Example.Com  ", "password123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_InactiveUser_ReturnsFalseAndDoesNotVerifyPassword()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = false
            });

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Authenticate("John@Example.Com", "password123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_WrongPassword_ReturnsFalseAndVerifiesPasswordWithStoredHash()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = true
            });
            hasherMock.Setup(h => h.Verify("wrong-password", "hashed-password")).Returns(false);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Authenticate("  JOHN@EXAMPLE.COM ", "wrong-password");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("wrong-password", "hashed-password"), Times.Once);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_CorrectPassword_ReturnsTrueAndUsesNormalizedLookup()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = true
            });
            hasherMock.Setup(h => h.Verify("password123", "hashed-password")).Returns(true);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Authenticate("  John@Example.Com  ", "password123");

            // Assert
            Assert.True(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("password123", "hashed-password"), Times.Once);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deactivate_InvalidEmail_ReturnsFalseAndDoesNotCallRepository(string? email)
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
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
            var result = service.Deactivate("  John@Example.Com  ");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Deactivate_ExistingUser_SetsInactiveAndSavesUser()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            var user = new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-password",
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(user);
            repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
                .Callback<User>(u => user = u);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.Deactivate("John@Example.Com");

            // Assert
            Assert.True(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john@example.com" &&
                u.IsActive == false)), Times.Once);
            Assert.False(user.IsActive);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserCount_EmptyRepository_ReturnsZero_Moq()
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserCount_MixedUsers_ReturnsOnlyActiveCount_Moq()
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserEmails_MixedUsers_ReturnsOrderedActiveEmailsOnly()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>
            {
                new User { Email = "z@example.com", IsActive = true },
                new User { Email = "a@example.com", IsActive = false },
                new User { Email = "m@example.com", IsActive = true },
                new User { Email = "b@example.com", IsActive = true }
            });

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Equal(new[] { "b@example.com", "m@example.com", "z@example.com" }, result);
            repositoryMock.Verify(r => r.GetAll(), Times.Once);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null, "oldPassword", "newPassword123")]
        [InlineData("", "oldPassword", "newPassword123")]
        [InlineData("   ", "oldPassword", "newPassword123")]
        [InlineData("john@example.com", "oldPassword", null)]
        [InlineData("john@example.com", "oldPassword", "")]
        [InlineData("john@example.com", "oldPassword", "short")]
        public void ChangePassword_InvalidInput_ReturnsFalseAndDoesNotCallDependencies(string? email, string oldPassword, string? newPassword)
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.ChangePassword(email!, oldPassword, newPassword!);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
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
            var result = service.ChangePassword("  John@Example.Com  ", "oldPassword", "newPassword123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_InactiveUser_ReturnsFalseAndDoesNotChangePassword()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-old",
                IsActive = false
            });

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.ChangePassword("john@example.com", "oldPassword", "newPassword123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_IncorrectOldPassword_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-old",
                IsActive = true
            });
            hasherMock.Setup(h => h.Verify("wrong-old", "hashed-old")).Returns(false);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.ChangePassword("  John@Example.Com  ", "wrong-old", "newPassword123");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("wrong-old", "hashed-old"), Times.Once);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_CorrectOldPassword_UpdatesHashAndSavesUser()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);

            var user = new User
            {
                Email = "john@example.com",
                Name = "John Doe",
                PasswordHash = "hashed-old",
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail("john@example.com")).Returns(user);
            hasherMock.Setup(h => h.Verify("oldPassword", "hashed-old")).Returns(true);
            hasherMock.Setup(h => h.Hash("newPassword123")).Returns("hashed-new");

            repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
                .Callback<User>(u => user = u);

            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.ChangePassword("John@Example.Com", "oldPassword", "newPassword123");

            // Assert
            Assert.True(result);
            repositoryMock.Verify(r => r.FindByEmail("john@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("oldPassword", "hashed-old"), Times.Once);
            hasherMock.Verify(h => h.Hash("newPassword123"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john@example.com" &&
                u.PasswordHash == "hashed-new" &&
                u.IsActive)), Times.Once);

            Assert.Equal("hashed-new", user.PasswordHash);
            Assert.True(user.IsActive);

            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }
    }
}