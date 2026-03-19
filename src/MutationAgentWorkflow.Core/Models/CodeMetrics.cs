namespace MutationAgentWorkflow.Core.Models;

public class CodeMetrics
{
    public int CyclomaticComplexity { get; set; }
    public int DependencyCount { get; set; }
    public List<string> InjectedDependencies { get; set; } = new();
    public bool HasExternalDependencies { get; set; }
    public bool IsControllerOrEndpoint { get; set; }
    public string RecommendedStrategy { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}
