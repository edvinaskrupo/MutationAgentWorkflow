namespace MutationAgentWorkflow.Core.Models;

public class MutationReport
{
    public double MutationScore { get; set; }
    public int TotalMutants { get; set; }
    public int KilledMutants { get; set; }
    public int SurvivedMutants { get; set; }
    public List<SurvivedMutant> SurvivedMutantDetails { get; set; } = new();
}

public class SurvivedMutant
{
    public string MutationType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string MutatedCode { get; set; } = string.Empty;
}