using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestImprovementAgent
{
    private readonly Kernel _kernel;
    public string Name => "Test Improvement Agent";

    public TestImprovementAgent(string apiKey, string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<string> ExecuteAsync(string input)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var prompt = $@"You are a test improvement expert. Analyze these survived mutants and suggest specific test improvements.

{input}

For each survived mutant, suggest:
1. What assertion to add or strengthen
2. What test case is missing
3. Concrete code changes

Be specific and actionable.";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "No improvements suggested";
    }

    public async Task<string> SuggestImprovementsAsync(MutationReport report, TestSuite currentTests)
    {
        var input = $@"CURRENT TESTS:
{currentTests.TestCode}

MUTATION SCORE: {report.MutationScore}%
SURVIVED MUTANTS: {report.SurvivedMutants}

DETAILS:
{string.Join("\n", report.SurvivedMutantDetails.Select(m =>
    $"- {m.MutationType} at {m.Location}: '{m.OriginalCode}' -> '{m.MutatedCode}'"))}";

        return await ExecuteAsync(input);
    }
}