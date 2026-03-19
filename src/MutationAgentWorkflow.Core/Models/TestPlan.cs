namespace MutationAgentWorkflow.Core.Models;

public class TestPlan
{
    public string Strategy { get; set; } = string.Empty; // "Unit", "Integration", or "Skip"
    public string Suggestion { get; set; } = string.Empty;
    public List<string> MethodsToTest { get; set; } = new();
    public List<string> DependenciesToMock { get; set; } = new();
    public CodeMetrics? Metrics { get; set; }
}