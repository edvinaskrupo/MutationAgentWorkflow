namespace MutationAgentWorkflow.Sample;

public interface IUserRepository
{
    bool ExistsByEmail(string email);
    void Save(User user);
    User? FindByEmail(string email);
    List<User> GetAll();
}

public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string hashedPassword);
}

public class User
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Service that manages user registration and authentication.
/// Depends on IUserRepository and IPasswordHasher (constructor injection).
/// Demonstrates the integration-test strategy path with Moq.
/// </summary>
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _hasher;

    public UserService(IUserRepository repository, IPasswordHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    public bool Register(string email, string name, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
            return false;

        if (plainPassword is null || plainPassword.Length < 8)
            return false;

        if (_repository.ExistsByEmail(email))
            return false;

        var user = new User
        {
            Email = email.Trim().ToLower(),
            Name = name.Trim(),
            PasswordHash = _hasher.Hash(plainPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _repository.Save(user);
        return true;
    }

    public bool Authenticate(string email, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(plainPassword))
            return false;

        var user = _repository.FindByEmail(email.Trim().ToLower());

        if (user is null || !user.IsActive)
            return false;

        return _hasher.Verify(plainPassword, user.PasswordHash);
    }

    public bool Deactivate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var user = _repository.FindByEmail(email.Trim().ToLower());

        if (user is null)
            return false;

        user.IsActive = false;
        _repository.Save(user);
        return true;
    }

    public int GetActiveUserCount()
    {
        var all = _repository.GetAll();
        return all.Count(u => u.IsActive);
    }

    public List<string> GetActiveUserEmails()
    {
        var all = _repository.GetAll();
        return all
            .Where(u => u.IsActive)
            .Select(u => u.Email)
            .OrderBy(e => e)
            .ToList();
    }

    public bool ChangePassword(string email, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        if (newPassword is null || newPassword.Length < 8)
            return false;

        var user = _repository.FindByEmail(email.Trim().ToLower());

        if (user is null || !user.IsActive)
            return false;

        if (!_hasher.Verify(oldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = _hasher.Hash(newPassword);
        _repository.Save(user);
        return true;
    }
}
