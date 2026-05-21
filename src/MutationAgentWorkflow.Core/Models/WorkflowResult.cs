namespace MutationAgentWorkflow.Core.Models;

public class WorkflowResult
{
    public double FinalMutationScore { get; set; }
    public int ValidationRetries { get; set; }
    public int TotalTestsGenerated { get; set; }
    public List<IterationArtifact> Artifacts { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
}