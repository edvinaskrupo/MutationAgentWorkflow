using MutationAgentWorkflow.Core.Models;
using MutationAgentWorkflow.Tools;

namespace MutationAgentWorkflow.Agents;

public class MutationAnalysisAgent
{
    private readonly StrykerRunner _strykerRunner = new();
    public string Name => "Mutation Analysis Agent";

    public async Task<MutationReport> RunAnalysisAsync(string testProjectPath, string sourceProjectPath)
    {
        return await _strykerRunner.RunMutationTestingAsync(testProjectPath, sourceProjectPath);
    }
}
