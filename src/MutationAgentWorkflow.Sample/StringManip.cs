namespace MutationAgentWorkflow.Sample;

public interface IStringRepository
{
    List<string> GetAll();
    void Save(string value);
    bool Exists(string value);
}

public interface IStringValidator
{
    bool IsValid(string value);
}

/// <summary>
/// A service that manages strings through a repository, with validation.
/// Designed to demonstrate integration-test strategy (constructor-injected dependencies).
/// </summary>
public class StringManipService
{
    private readonly IStringRepository _repository;
    private readonly IStringValidator _validator;

    public StringManipService(IStringRepository repository, IStringValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public bool AddString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!_validator.IsValid(value))
            return false;

        if (_repository.Exists(value))
            return false;

        _repository.Save(value);
        return true;
    }

    public List<string> GetFilteredStrings(int minLength)
    {
        if (minLength < 0)
            throw new ArgumentException("Minimum length cannot be negative.", nameof(minLength));

        var all = _repository.GetAll();
        return all.Where(s => s.Length >= minLength).ToList();
    }

    public int CountValidStrings()
    {
        var all = _repository.GetAll();
        return all.Count(s => _validator.IsValid(s));
    }

    public string? FindLongestValid()
    {
        var all = _repository.GetAll();
        return all
            .Where(s => _validator.IsValid(s))
            .OrderByDescending(s => s.Length)
            .FirstOrDefault();
    }

    public Dictionary<char, int> GetCharacterFrequency()
    {
        var all = _repository.GetAll();
        var freq = new Dictionary<char, int>();

        foreach (var str in all)
        {
            foreach (var c in str.ToLower())
            {
                if (char.IsLetterOrDigit(c))
                    freq[c] = freq.GetValueOrDefault(c) + 1;
            }
        }

        return freq;
    }
}
