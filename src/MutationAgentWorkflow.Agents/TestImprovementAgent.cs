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

    public async Task<string> ImproveTestsAsync(MutationReport report, TestSuite currentTests, CodeUnderTest code, TestPlan plan)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var survivedDetails = string.Join("\n", report.SurvivedMutantDetails.Select(m =>
            $"- {m.MutationType} at {m.Location}: '{m.OriginalCode}' -> '{m.MutatedCode}'"));

        var mockingNote = plan.Strategy == "Integration"
            ? "Use Moq (Mock<T>) for all injected dependencies. Include 'using Moq;'."
            : "This is a unit test class. Do NOT use any mocking framework.";

        var prompt = $@"You are a test improvement expert. Your task is to improve the existing test code so that it kills the survived mutants listed below.

SOURCE CODE UNDER TEST:
{code.SourceCode}

CURRENT TEST CODE:
{currentTests.TestCode}

MUTATION SCORE: {report.MutationScore}%
TOTAL MUTANTS: {report.TotalMutants}
KILLED: {report.KilledMutants}
SURVIVED: {report.SurvivedMutants}

SURVIVED MUTANT DETAILS:
{survivedDetails}

TEST STRATEGY: {plan.Strategy}
{mockingNote}

STRICT REQUIREMENTS:
1. Return the COMPLETE, improved test class — not just the changes.
2. Keep all existing tests that already pass and kill mutants.
3. Add new test methods or strengthen assertions to kill survived mutants.
4. Every test method MUST use explicit // Arrange, // Act, // Assert comment sections.
5. Use descriptive method names: MethodName_Scenario_ExpectedBehavior.
6. Include all necessary using statements.

Generate ONLY the complete improved test class code. No explanations, no markdown fences.";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        var improvedCode = result.Content ?? currentTests.TestCode;

        return StripMarkdownFences(improvedCode);
    }

    private static string StripMarkdownFences(string code)
    {
        var lines = code.Split('\n').ToList();
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("```"))
            lines.RemoveAt(0);
        if (lines.Count > 0 && lines[^1].TrimStart().StartsWith("```"))
            lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }
}
