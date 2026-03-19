using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestGenerationAgent
{
    private readonly Kernel _kernel;
    public string Name => "Test Generation Agent";

    public TestGenerationAgent(string apiKey, string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<TestSuite> GenerateTestsAsync(TestPlan plan, CodeUnderTest code)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var prompt = BuildPrompt(plan, code);

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        var testCode = result.Content ?? "// No tests generated";

        testCode = StripMarkdownFences(testCode);

        return new TestSuite
        {
            TestCode = testCode,
            TestFilePath = $"{code.ClassName}Tests.cs"
        };
    }

    private string BuildPrompt(TestPlan plan, CodeUnderTest code)
    {
        var metricsSection = "";
        if (plan.Metrics is not null)
        {
            metricsSection = $@"
CODE METRICS:
- Cyclomatic complexity: {plan.Metrics.CyclomaticComplexity}
- Injected dependencies: {(plan.Metrics.InjectedDependencies.Count > 0 ? string.Join(", ", plan.Metrics.InjectedDependencies) : "None")}
- Is controller/endpoint: {plan.Metrics.IsControllerOrEndpoint}
";
        }

        var mockingInstructions = plan.Strategy == "Integration"
            ? BuildMockingInstructions(plan)
            : "This is a pure logic class with no external dependencies. Do NOT use any mocking framework.";

        return $@"You are an expert C# test developer. Generate xUnit tests for the code below.

TEST STRATEGY: {plan.Strategy}
{metricsSection}
PLANNING SUGGESTIONS:
{plan.Suggestion}

CODE TO TEST:
{code.SourceCode}

STRICT REQUIREMENTS:
1. Use the xUnit framework.
2. Every test method MUST be structured with explicit comment sections:
   // Arrange
   // Act
   // Assert
3. Use descriptive test method names following the pattern: MethodName_Scenario_ExpectedBehavior.
4. Use [Fact] for single-case tests and [Theory] with [InlineData(...)] for parameterized tests.
5. Test both happy paths and edge cases (null inputs, boundary values, empty collections).
6. Include all necessary using statements at the top of the file.

{mockingInstructions}

Generate ONLY the complete test class code. No explanations, no markdown fences.";
    }

    private string BuildMockingInstructions(TestPlan plan)
    {
        var deps = plan.Metrics?.InjectedDependencies ?? new List<string>();
        var depsList = deps.Count > 0
            ? string.Join(", ", deps)
            : "identified dependencies";

        return $@"MOCKING REQUIREMENTS (Integration tests):
- Use the Moq library (Mock<T>) to mock all injected dependencies: {depsList}.
- Include ""using Moq;"" at the top.
- In the Arrange section, create mocks, set up behaviors with .Setup(...), and inject them into the class constructor.
- Verify mock interactions with .Verify(...) where appropriate in the Assert section.";
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
