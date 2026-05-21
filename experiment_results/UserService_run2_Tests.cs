using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using MutationAgentWorkflow.Sample;

namespace MutationAgentWorkflow.Tests
{
    public class UserServiceTests
    {
        private sealed class FakeUserRepository : IUserRepository
        {
            public Func<string, bool>? ExistsByEmailFunc { get; set; }
            public Func<string, User?>? FindByEmailFunc { get; set; }
            public Func<List<User>>? GetAllFunc { get; set; }

            public List<User> SavedUsers { get; } = new();
            public List<string> ExistsByEmailCalls { get; } = new();
            public List<string> FindByEmailCalls { get; } = new();

            public bool ExistsByEmail(string email)
            {
                ExistsByEmailCalls.Add(email);
                return ExistsByEmailFunc?.Invoke(email) ?? false;
            }

            public void Save(User user)
            {
                SavedUsers.Add(user);
            }

            public User? FindByEmail(string email)
            {
                FindByEmailCalls.Add(email);
                return FindByEmailFunc?.Invoke(email);
            }

            public List<User> GetAll()
            {
                return GetAllFunc?.Invoke() ?? new List<User>();
            }
        }

        private sealed class FakePasswordHasher : IPasswordHasher
        {
            public Func<string, string>? HashFunc { get; set; }
            public Func<string, string, bool>? VerifyFunc { get; set; }

            public List<string> HashCalls { get; } = new();
            public List<(string PlainPassword, string HashedPassword)> VerifyCalls { get; } = new();

            public string Hash(string plainPassword)
            {
                HashCalls.Add(plainPassword);
                return HashFunc?.Invoke(plainPassword) ?? $"HASHED:{plainPassword}";
            }

            public bool Verify(string plainPassword, string hashedPassword)
            {
                VerifyCalls.Add((plainPassword, hashedPassword));
                return VerifyFunc?.Invoke(plainPassword, hashedPassword) ?? false;
            }
        }

        [Theory]
        [InlineData("  Test@Email.com  ", "  Alice  ", "password1", "test@email.com", "Alice")]
        [InlineData("USER@EXAMPLE.COM", "Bob", "12345678", "user@example.com", "Bob")]
        public void Register_ValidInput_SavesNormalizedUserAndReturnsTrue(string email, string name, string password, string expectedEmail, string expectedName)
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                ExistsByEmailFunc = _ => false
            };
            var hasher = new FakePasswordHasher
            {
                HashFunc = plain => $"hashed-{plain}"
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register(email, name, password);

            // Assert
            Assert.True(result);
            Assert.Single(repository.SavedUsers);
            Assert.Equal(expectedEmail, repository.SavedUsers[0].Email);
            Assert.Equal(expectedName, repository.SavedUsers[0].Name);
            Assert.Equal($"hashed-{password}", repository.SavedUsers[0].PasswordHash);
            Assert.True(repository.SavedUsers[0].IsActive);
            Assert.Equal(password, hasher.HashCalls.Single());
        }

        [Theory]
        [InlineData(null, "Alice", "password1")]
        [InlineData("", "Alice", "password1")]
        [InlineData("   ", "Alice", "password1")]
        [InlineData("test@example.com", null, "password1")]
        [InlineData("test@example.com", "", "password1")]
        [InlineData("test@example.com", "   ", "password1")]
        public void Register_WhitespaceOrNullEmailOrName_ReturnsFalse(string? email, string? name, string password)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register(email!, name!, password);

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Register_NullOrEmptyPassword_ReturnsFalse(string? password)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("test@example.com", "Alice", password!);

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Theory]
        [InlineData("1234567")]
        [InlineData("abcdefg")]
        public void Register_PasswordShorterThanMinimumLength_ReturnsFalse(string password)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("test@example.com", "Alice", password);

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void Register_PasswordExactlyMinimumLength_Succeeds()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                ExistsByEmailFunc = _ => false
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("test@example.com", "Alice", "12345678");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Register_DuplicateEmail_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                ExistsByEmailFunc = _ => true
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Register("test@example.com", "Alice", "password1");

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void Authenticate_ValidCredentials_ReturnsTrue()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => new User
                {
                    Email = "test@example.com",
                    PasswordHash = "hash",
                    IsActive = true
                }
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (plain, hash) => plain == "password1" && hash == "hash"
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate("  TEST@example.com  ", "password1");

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(null, "password1")]
        [InlineData("", "password1")]
        [InlineData("   ", "password1")]
        [InlineData("test@example.com", null)]
        [InlineData("test@example.com", "")]
        [InlineData("test@example.com", "   ")]
        public void Authenticate_WhitespaceOrNullEmailOrPassword_ReturnsFalse(string? email, string? password)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate(email!, password!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => null
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate("test@example.com", "password1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_InactiveUser_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => new User
                {
                    Email = "test@example.com",
                    PasswordHash = "hash",
                    IsActive = false
                }
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate("test@example.com", "password1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Authenticate_IncorrectPassword_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => new User
                {
                    Email = "test@example.com",
                    PasswordHash = "hash",
                    IsActive = true
                }
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (plain, hash) => false
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Authenticate("test@example.com", "wrongpass");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Deactivate_ExistingUser_SetsInactiveAndSaves()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                IsActive = true
            };
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => user
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate("  TEST@example.com  ");

            // Assert
            Assert.True(result);
            Assert.False(user.IsActive);
            Assert.Single(repository.SavedUsers);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deactivate_WhitespaceOrNullEmail_ReturnsFalse(string? email)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate(email!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Deactivate_UserNotFound_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => null
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.Deactivate("test@example.com");

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void GetActiveUserCount_NoUsers_ReturnsZero()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                GetAllFunc = () => new List<User>()
            };
            var hasher = new FakePasswordHasher();
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
            var repository = new FakeUserRepository
            {
                GetAllFunc = () => new List<User>
                {
                    new User { IsActive = true },
                    new User { IsActive = false },
                    new User { IsActive = true }
                }
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserCount();

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void GetActiveUserEmails_NoUsers_ReturnsEmptyList()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                GetAllFunc = () => new List<User>()
            };
            var hasher = new FakePasswordHasher();
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
            var repository = new FakeUserRepository
            {
                GetAllFunc = () => new List<User>
                {
                    new User { Email = "b@example.com", IsActive = false },
                    new User { Email = "a@example.com", IsActive = false }
                }
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetActiveUserEmails_ActiveUsers_ReturnsSortedEmails()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                GetAllFunc = () => new List<User>
                {
                    new User { Email = "c@example.com", IsActive = true },
                    new User { Email = "a@example.com", IsActive = true },
                    new User { Email = "b@example.com", IsActive = false },
                    new User { Email = "b@example.com", IsActive = true }
                }
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Equal(new[] { "a@example.com", "b@example.com", "c@example.com" }, result);
        }

        [Fact]
        public void ChangePassword_ValidInput_UpdatesPasswordAndSaves()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "old-hash",
                IsActive = true
            };
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => user
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (plain, hash) => plain == "oldPassword1" && hash == "old-hash",
                HashFunc = plain => $"new-hash-{plain}"
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("  TEST@example.com  ", "oldPassword1", "newPassword1");

            // Assert
            Assert.True(result);
            Assert.Equal("new-hash-newPassword1", user.PasswordHash);
            Assert.Single(repository.SavedUsers);
        }

        [Theory]
        [InlineData(null, "oldPassword1", "newPassword1")]
        [InlineData("", "oldPassword1", "newPassword1")]
        [InlineData("   ", "oldPassword1", "newPassword1")]
        public void ChangePassword_WhitespaceOrNullEmail_ReturnsFalse(string? email, string oldPassword, string newPassword)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword(email!, oldPassword, newPassword);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ChangePassword_NullOrShortNewPassword_ReturnsFalse(string? newPassword)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", newPassword!);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("1234567")]
        [InlineData("abcdefg")]
        public void ChangePassword_NewPasswordShorterThanMinimumLength_ReturnsFalse(string newPassword)
        {
            // Arrange
            var repository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", newPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ChangePassword_NewPasswordExactlyMinimumLength_Succeeds()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "old-hash",
                IsActive = true
            };
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => user
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (_, _) => true,
                HashFunc = plain => $"hashed-{plain}"
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", "12345678");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ChangePassword_UserNotFound_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => null
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", "newPassword1");

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void ChangePassword_InactiveUser_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => new User
                {
                    Email = "test@example.com",
                    PasswordHash = "old-hash",
                    IsActive = false
                }
            };
            var hasher = new FakePasswordHasher();
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", "newPassword1");

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void ChangePassword_WrongOldPassword_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => new User
                {
                    Email = "test@example.com",
                    PasswordHash = "old-hash",
                    IsActive = true
                }
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (_, _) => false
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "wrongOldPassword", "newPassword1");

            // Assert
            Assert.False(result);
            Assert.Empty(repository.SavedUsers);
        }

        [Fact]
        public void ChangePassword_OldPasswordVerificationSucceeds_HashesNewPassword()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "old-hash",
                IsActive = true
            };
            var repository = new FakeUserRepository
            {
                FindByEmailFunc = _ => user
            };
            var hasher = new FakePasswordHasher
            {
                VerifyFunc = (_, _) => true,
                HashFunc = plain => $"hash-{plain}"
            };
            var service = new UserService(repository, hasher);

            // Act
            var result = service.ChangePassword("test@example.com", "oldPassword1", "newPassword1");

            // Assert
            Assert.True(result);
            Assert.Equal("hash-newPassword1", user.PasswordHash);
        }
    }
}

namespace MutationAgentWorkflow.Sample.Tests
{
    public class UserServiceIntegrationTests
    {
        [Theory]
        [InlineData(null, "John Doe", "password1")]
        [InlineData("", "John Doe", "password1")]
        [InlineData("   ", "John Doe", "password1")]
        [InlineData("john@example.com", null, "password1")]
        [InlineData("john@example.com", "", "password1")]
        [InlineData("john@example.com", "   ", "password1")]
        [InlineData("john@example.com", "John Doe", null)]
        [InlineData("john@example.com", "John Doe", "short")]
        public void Register_InvalidInputs_ExpectedBehavior(string? email, string? name, string? plainPassword)
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Register_DuplicateEmail_ExpectedBehavior()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputName = "  John Doe  ";
            var inputPassword = "password123";

            repositoryMock.Setup(r => r.ExistsByEmail(inputEmail)).Returns(true);

            // Act
            var result = service.Register(inputEmail, inputName, inputPassword);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.ExistsByEmail(inputEmail), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Register_ValidInputs_SavesNormalizedActiveUserAndReturnsTrue()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputName = "  John Doe  ";
            var inputPassword = "password123";
            var hashedPassword = "hashed-password123";

            repositoryMock.Setup(r => r.ExistsByEmail(inputEmail)).Returns(false);
            hasherMock.Setup(h => h.Hash(inputPassword)).Returns(hashedPassword);
            repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
                .Callback<User>(_ => { });

            User? savedUser = null;
            repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
                .Callback<User>(u => savedUser = u);

            // Act
            var result = service.Register(inputEmail, inputName, inputPassword);

            // Assert
            Assert.True(result);
            Assert.NotNull(savedUser);
            Assert.Equal("john.doe@example.com", savedUser!.Email);
            Assert.Equal("John Doe", savedUser.Name);
            Assert.Equal(hashedPassword, savedUser.PasswordHash);
            Assert.True(savedUser.IsActive);
            Assert.True((DateTime.UtcNow - savedUser.CreatedAt).TotalSeconds < 5);

            repositoryMock.Verify(r => r.ExistsByEmail(inputEmail), Times.Once);
            hasherMock.Verify(h => h.Hash(inputPassword), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john.doe@example.com" &&
                u.Name == "John Doe" &&
                u.PasswordHash == hashedPassword &&
                u.IsActive &&
                (DateTime.UtcNow - u.CreatedAt).TotalSeconds < 5)), Times.Once);

            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john.doe@example.com" &&
                u.Name == "John Doe" &&
                u.PasswordHash == hashedPassword &&
                u.IsActive)), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null, "password123")]
        [InlineData("", "password123")]
        [InlineData("   ", "password123")]
        public void Authenticate_InvalidEmailOrPassword_ExpectedBehavior(string? email, string? plainPassword)
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
        public void Authenticate_UserNotFound_ReturnsFalseWithoutPasswordVerification()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputPassword = "password123";

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns((User?)null);

            // Act
            var result = service.Authenticate(inputEmail, inputPassword);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_InactiveUser_ReturnsFalseWithoutPasswordVerification()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputPassword = "password123";
            var inactiveUser = new User
            {
                Email = "john.doe@example.com",
                Name = "John Doe",
                PasswordHash = "hashed",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = false
            };

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns(inactiveUser);

            // Act
            var result = service.Authenticate(inputEmail, inputPassword);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_ValidCredentials_VerifiesPasswordAndReturnsTrue()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputPassword = "password123";
            var normalizedEmail = "john.doe@example.com";
            var passwordHash = "hashed-password123";
            var user = new User
            {
                Email = normalizedEmail,
                Name = "John Doe",
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail(normalizedEmail)).Returns(user);
            hasherMock.Setup(h => h.Verify(inputPassword, passwordHash)).Returns(true);

            // Act
            var result = service.Authenticate(inputEmail, inputPassword);

            // Assert
            Assert.True(result);
            repositoryMock.Verify(r => r.FindByEmail(normalizedEmail), Times.Once);
            hasherMock.Verify(h => h.Verify(inputPassword, passwordHash), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Authenticate_InvalidPassword_ReturnsFalseAfterVerification()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var inputEmail = "  John.Doe@Example.Com  ";
            var inputPassword = "password123";
            var normalizedEmail = "john.doe@example.com";
            var passwordHash = "hashed-password123";
            var user = new User
            {
                Email = normalizedEmail,
                Name = "John Doe",
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail(normalizedEmail)).Returns(user);
            hasherMock.Setup(h => h.Verify(inputPassword, passwordHash)).Returns(false);

            // Act
            var result = service.Authenticate(inputEmail, inputPassword);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail(normalizedEmail), Times.Once);
            hasherMock.Verify(h => h.Verify(inputPassword, passwordHash), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
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
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Deactivate_MissingUser_ReturnsFalseWithoutSave()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns((User?)null);

            // Act
            var result = service.Deactivate("  John.Doe@Example.Com  ");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Deactivate_ExistingUser_SetsInactiveAndSavesUser()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var user = new User
            {
                Email = "john.doe@example.com",
                Name = "John Doe",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns(user);
            repositoryMock.Setup(r => r.Save(It.IsAny<User>())).Callback<User>(u => user = u);

            // Act
            var result = service.Deactivate("  John.Doe@Example.Com  ");

            // Assert
            Assert.True(result);
            Assert.False(user.IsActive);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john.doe@example.com" &&
                u.IsActive == false)), Times.Once);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserCount_EmptyRepository_ReturnsZero()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());

            // Act
            var result = service.GetActiveUserCount();

            // Assert
            Assert.Equal(0, result);
            repositoryMock.Verify(r => r.GetAll(), Times.Once);
            repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserCount_MixedUsers_ReturnsOnlyActiveCount()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>
            {
                new User { Email = "a@example.com", IsActive = true },
                new User { Email = "b@example.com", IsActive = false },
                new User { Email = "c@example.com", IsActive = true }
            });

            // Act
            var result = service.GetActiveUserCount();

            // Assert
            Assert.Equal(2, result);
            repositoryMock.Verify(r => r.GetAll(), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserEmails_NoUsers_ReturnsEmptyOrderedList()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            repositoryMock.Verify(r => r.GetAll(), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetActiveUserEmails_MixedUsers_ReturnsSortedDistinctOrderPreservedBySourceFiltering()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.GetAll()).Returns(new List<User>
            {
                new User { Email = "z@example.com", IsActive = true },
                new User { Email = "a@example.com", IsActive = false },
                new User { Email = "b@example.com", IsActive = true },
                new User { Email = "a@example.com", IsActive = true }
            });

            // Act
            var result = service.GetActiveUserEmails();

            // Assert
            Assert.Equal(new[] { "a@example.com", "b@example.com", "z@example.com" }, result);
            repositoryMock.Verify(r => r.GetAll(), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            repositoryMock.Verify(r => r.FindByEmail(It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null, "oldpassword", "newpassword1")]
        [InlineData("", "oldpassword", "newpassword1")]
        [InlineData("   ", "oldpassword", "newpassword1")]
        [InlineData("john@example.com", "oldpassword", null)]
        [InlineData("john@example.com", "oldpassword", "short")]
        public void ChangePassword_InvalidInputs_ExpectedBehavior(string? email, string oldPassword, string? newPassword)
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            // Act
            var result = service.ChangePassword(email!, oldPassword!, newPassword!);

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
        public void ChangePassword_MissingUser_ReturnsFalseWithoutVerificationOrSave()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns((User?)null);

            // Act
            var result = service.ChangePassword("  John.Doe@Example.Com  ", "oldpassword", "newpassword1");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_InactiveUser_ReturnsFalseWithoutVerificationOrSave()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var user = new User
            {
                Email = "john.doe@example.com",
                Name = "John Doe",
                PasswordHash = "oldhash",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = false
            };

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns(user);

            // Act
            var result = service.ChangePassword("  John.Doe@Example.Com  ", "oldpassword", "newpassword1");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_WrongOldPassword_ReturnsFalseWithoutSaving()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var user = new User
            {
                Email = "john.doe@example.com",
                Name = "John Doe",
                PasswordHash = "oldhash",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns(user);
            hasherMock.Setup(h => h.Verify("wrong-old", "oldhash")).Returns(false);

            // Act
            var result = service.ChangePassword("  John.Doe@Example.Com  ", "wrong-old", "newpassword1");

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("wrong-old", "oldhash"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
            hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ChangePassword_ValidInputs_UpdatesHashAndSavesUser()
        {
            // Arrange
            var repositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
            var hasherMock = new Mock<IPasswordHasher>(MockBehavior.Strict);
            var service = new UserService(repositoryMock.Object, hasherMock.Object);

            var user = new User
            {
                Email = "john.doe@example.com",
                Name = "John Doe",
                PasswordHash = "oldhash",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            repositoryMock.Setup(r => r.FindByEmail("john.doe@example.com")).Returns(user);
            hasherMock.Setup(h => h.Verify("oldpassword", "oldhash")).Returns(true);
            hasherMock.Setup(h => h.Hash("newpassword1")).Returns("newhash");
            repositoryMock.Setup(r => r.Save(It.IsAny<User>())).Verifiable();

            User? savedUser = null;
            repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
                .Callback<User>(u => savedUser = u);

            // Act
            var result = service.ChangePassword("  John.Doe@Example.Com  ", "oldpassword", "newpassword1");

            // Assert
            Assert.True(result);
            Assert.NotNull(savedUser);
            Assert.Equal("john.doe@example.com", savedUser!.Email);
            Assert.Equal("newhash", savedUser.PasswordHash);
            Assert.True(savedUser.IsActive);

            repositoryMock.Verify(r => r.FindByEmail("john.doe@example.com"), Times.Once);
            hasherMock.Verify(h => h.Verify("oldpassword", "oldhash"), Times.Once);
            hasherMock.Verify(h => h.Hash("newpassword1"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john.doe@example.com" &&
                u.PasswordHash == "newhash" &&
                u.IsActive)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<User>(u =>
                u.Email == "john.doe@example.com" &&
                u.PasswordHash == "newhash" &&
                u.IsActive)), Times.Once);
            repositoryMock.Verify(r => r.ExistsByEmail(It.IsAny<string>()), Times.Never);
            hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            repositoryMock.VerifyNoOtherCalls();
            hasherMock.VerifyNoOtherCalls();
        }
    }
}