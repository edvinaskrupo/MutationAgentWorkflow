using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestImprovementAgent
{
    private readonly Kernel _kernel;
    public string Name => "Test Improvement Agent";

    public TestImprovementAgent(string apiKey, string model = "gpt-5.4-mini")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<string> FixTestsAsync(
        string currentTestCode,
        CodeUnderTest code,
        TestPlan plan,
        string errorOutput,
        bool isBuildError)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var errorType = isBuildError ? "COMPILATION" : "TEST EXECUTION";
        var fixFocus = isBuildError
            ? "Fix ALL compilation errors. Ensure using statements, type names, and method signatures match the source code exactly."
            : "Fix the failing tests. Ensure each test's assertions match the actual behavior of the source code. Do NOT change the source code, only the tests.";

        var mockingNote = plan.Strategy == "Both" || plan.Strategy == "Integration"
            ? "If the test file contains integration tests using Moq (Mock<T>), ensure mock setups match the actual interface signatures. Include 'using Moq;'."
            : "This test file contains only unit tests. Do NOT use any mocking framework.";

        var prompt = $@"You are a test repair expert. The test code below has {errorType} ERRORS that must be fixed.

SOURCE CODE UNDER TEST:
{code.SourceCode}

CURRENT TEST CODE (contains errors):
{currentTestCode}

ERROR OUTPUT:
{errorOutput}

TEST STRATEGY: {plan.Strategy}
{mockingNote}

TASK: {fixFocus}

STRICT REQUIREMENTS:
1. Return the COMPLETE, fixed test class — not just the changes.
2. Keep all tests that already work. Only fix the broken ones.
3. Every test method MUST use explicit // Arrange, // Act, // Assert comment sections.
4. Use descriptive method names: MethodName_Scenario_ExpectedBehavior.
5. Include all necessary using statements.
6. Do NOT remove tests — fix them or replace broken ones with correct equivalents.

Generate ONLY the complete fixed test class code. No explanations, no markdown fences.";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        var fixedCode = result.Content ?? currentTestCode;

        return StripMarkdownFences(fixedCode);
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
