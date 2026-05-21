namespace MutationAgentWorkflow.Core.Models;

public class IterationArtifact
{
    public int Iteration { get; set; }
    public string UnitTestCode { get; set; } = string.Empty;
    public string IntegrationTestCode { get; set; } = string.Empty;
    public string MergedTestCode { get; set; } = string.Empty;
    public bool BuildSucceeded { get; set; }
    public bool TestsPass { get; set; }
    public int AnalyzerWarnings { get; set; }
    public TimeSpan TestRunDuration { get; set; }
    public string ErrorOutput { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
