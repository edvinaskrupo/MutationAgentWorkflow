namespace MutationAgentWorkflow.Sample;

/// <summary>
/// Pure logic class for password validation. No external dependencies.
/// Demonstrates the unit-test strategy path.
/// </summary>
public class PasswordValidator
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "qwerty", "abc123",
        "letmein", "admin", "welcome", "monkey", "master"
    };

    public const int MinLength = 8;
    public const int MaxLength = 64;

    public bool IsValid(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (password.Length < MinLength || password.Length > MaxLength)
            return false;

        if (!HasUpperCase(password))
            return false;

        if (!HasLowerCase(password))
            return false;

        if (!HasDigit(password))
            return false;

        if (!HasSpecialCharacter(password))
            return false;

        if (IsCommonPassword(password))
            return false;

        return true;
    }

    public int GetStrengthScore(string password)
    {
        if (string.IsNullOrEmpty(password))
            return 0;

        int score = 0;

        if (password.Length >= MinLength)
            score += 1;

        if (password.Length >= 12)
            score += 1;

        if (password.Length >= 16)
            score += 1;

        if (HasUpperCase(password))
            score += 1;

        if (HasLowerCase(password))
            score += 1;

        if (HasDigit(password))
            score += 1;

        if (HasSpecialCharacter(password))
            score += 1;

        if (!IsCommonPassword(password))
            score += 1;

        if (CountUniqueCharacters(password) >= 8)
            score += 1;

        return score;
    }

    public string GetStrengthLabel(string password)
    {
        var score = GetStrengthScore(password);

        return score switch
        {
            <= 2 => "Weak",
            <= 5 => "Fair",
            <= 7 => "Strong",
            _ => "Very Strong"
        };
    }

    public List<string> GetValidationErrors(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password cannot be empty.");
            return errors;
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters.");

        if (password.Length > MaxLength)
            errors.Add($"Password must be at most {MaxLength} characters.");

        if (!HasUpperCase(password))
            errors.Add("Password must contain at least one uppercase letter.");

        if (!HasLowerCase(password))
            errors.Add("Password must contain at least one lowercase letter.");

        if (!HasDigit(password))
            errors.Add("Password must contain at least one digit.");

        if (!HasSpecialCharacter(password))
            errors.Add("Password must contain at least one special character.");

        if (IsCommonPassword(password))
            errors.Add("Password is too common.");

        return errors;
    }

    public bool HasUpperCase(string input) => input.Any(char.IsUpper);

    public bool HasLowerCase(string input) => input.Any(char.IsLower);

    public bool HasDigit(string input) => input.Any(char.IsDigit);

    public bool HasSpecialCharacter(string input) =>
        input.Any(c => !char.IsLetterOrDigit(c));

    public bool IsCommonPassword(string password) =>
        CommonPasswords.Contains(password);

    public int CountUniqueCharacters(string input) =>
        input.Distinct().Count();
}
