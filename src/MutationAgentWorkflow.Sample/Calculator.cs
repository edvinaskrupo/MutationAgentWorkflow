namespace MutationAgentWorkflow.Sample;

public class Calculator
{
    public string ReverseString(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        return new string(input.Reverse().ToArray());
    }

    public bool IsPalindrome(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        var cleaned = input.ToLower().Where(char.IsLetterOrDigit).ToArray();
        return cleaned.SequenceEqual(cleaned.Reverse());
    }

    public List<string> ChunkString(string input, int chunkSize)
    {
        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be greater than zero");
        var chunks = new List<string>();
        for (int i = 0; i < input.Length; i += chunkSize)
            chunks.Add(input.Substring(i, Math.Min(chunkSize, input.Length - i)));
        return chunks;
    }

    public int CountVowels(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        return input.Count(c => "aeiouAEIOU".Contains(c));
    }

    public string TitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be null or whitespace");
        return string.Join(" ", input.Split(' ')
            .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w.Substring(1).ToLower()));
    }

    public bool IsAnagram(string a, string b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException("Inputs cannot be null");
        return a.ToLower().OrderBy(c => c).SequenceEqual(b.ToLower().OrderBy(c => c));
    }
}