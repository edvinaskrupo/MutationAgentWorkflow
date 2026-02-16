namespace MutationAgentWorkflow.Core.Models;

public class WorkflowResult
{
    public double InitialMutationScore { get; set; }
    public double FinalMutationScore { get; set; }
    public int IterationsCompleted { get; set; }
    public int TotalTestsGenerated { get; set; }
    public List<string> ImprovementActions { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
}