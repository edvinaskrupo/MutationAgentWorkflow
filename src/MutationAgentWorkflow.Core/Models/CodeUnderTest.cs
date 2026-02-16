namespace MutationAgentWorkflow.Core.Models;

public class CodeUnderTest
{
    public string FilePath { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public List<string> Methods { get; set; } = new();
}