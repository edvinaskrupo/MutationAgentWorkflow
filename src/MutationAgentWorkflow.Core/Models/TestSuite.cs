namespace MutationAgentWorkflow.Core.Models;

public class TestSuite
{
    public string TestCode { get; set; } = string.Empty;
    public string TestFilePath { get; set; } = string.Empty;
    public bool CompilesSuccessfully { get; set; }
    public bool AllTestsPass { get; set; }
    public List<string> CompilationErrors { get; set; } = new();
    public int TestCount { get; set; }
}