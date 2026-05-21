using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class UnitTestGenerationAgent
{
    private readonly Kernel _kernel;
    public string Name => "Unit Test Generation Agent";

    public UnitTestGenerationAgent(string apiKey, string model = "gpt-5.4-mini")
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
            TestFilePath = $"{code.ClassName}UnitTests.cs",
            TestType = "Unit"
        };
    }

    private string BuildPrompt(TestPlan plan, CodeUnderTest code)
    {
        var metricsSection = "";
        if (plan.Metrics is not null)
        {
            var perMethodCc = plan.Metrics.MethodComplexities.Count > 0
                ? string.Join(", ", plan.Metrics.MethodComplexities
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}: {kv.Value}"))
                : "N/A";

            metricsSection = $@"
CODE METRICS:
- Total cyclomatic complexity: {plan.Metrics.CyclomaticComplexity}
- Per-method complexity (highest first): {perMethodCc}
- This is a pure logic class with no external dependencies.
";
        }

        return $@"You are an expert C# unit test developer. Generate xUnit UNIT tests for the code below.

TEST STRATEGY: Unit (kodo vieneto testai)
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
7. Do NOT use any mocking framework. This is a pure unit test class.
8. Focus on boundary value analysis: test exact boundary values (e.g., length == min, length == max, length == min-1, length == max+1).
9. Every Assert must verify a single, specific behavior.

Generate ONLY the complete test class code. No explanations, no markdown fences.";
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
