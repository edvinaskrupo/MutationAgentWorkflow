namespace MutationAgentWorkflow.Core.Models;

public class TestPlan
{
    public string Strategy { get; set; } = string.Empty; // "Unit" or "Integration"
    public string Suggestion { get; set; } = string.Empty;
    public List<string> MethodsToTest { get; set; } = new();
    public List<string> DependenciesToMock { get; set; } = new();
}