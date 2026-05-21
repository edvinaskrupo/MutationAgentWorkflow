using Microsoft.Extensions.Configuration;
using MutationAgentWorkflow.Agents;
using MutationAgentWorkflow.Core.Models;
using MutationAgentWorkflow.Tools;
using System.Diagnostics;
using System.Text.Json;

namespace MutationAgentWorkflow.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== Mutation-Guided Agentic Test Generation Workflow ===\n");

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var apiKey = config["OpenAI:ApiKey"] ?? throw new Exception("OpenAI API key not found in appsettings.json");
        var model = config["OpenAI:Model"] ?? "gpt-5.4-mini";
        var maxRetries = int.TryParse(config["Workflow:MaxRetries"], out var mr) ? mr : 3;
        var runtimeThreshold = int.TryParse(config["Workflow:RuntimeThresholdSeconds"], out var rt) ? rt : 60;
        var targetScore = double.TryParse(config["Workflow:TargetMutationScore"], out var ts) ? ts : 80.0;

        var codeUnderTest = await LoadCodeUnderTestAsync(config);

        var planningAgent = new TestPlanningAgent(apiKey, model);
        var unitAgent = new UnitTestGenerationAgent(apiKey, model);
        var integrationAgent = new IntegrationTestGenerationAgent(apiKey, model);
        var improvementAgent = new TestImprovementAgent(apiKey, model);
        var mutationAgent = new MutationAnalysisAgent();
        var scaffolder = new TestProjectScaffolder();

        var stopwatch = Stopwatch.StartNew();
        var workflowResult = new WorkflowResult();

        try
        {
            // ===== STAGE 1: Analysis =====
            PrintStageHeader(1, "Code Analysis & Test Planning");

            var testPlan = await planningAgent.GeneratePlanAsync(codeUnderTest);
            PrintMetrics(testPlan);

            if (testPlan.Strategy == "Skip")
            {
                System.Console.WriteLine("  Strategy: SKIP — no meaningful logic to test.");
                System.Console.WriteLine($"  Reason: {testPlan.Metrics?.Reasoning}\n");
                System.Console.WriteLine("Workflow complete — nothing to test.");
                return;
            }

            System.Console.WriteLine($"  AI Suggestions:\n{Indent(testPlan.Suggestion, 4)}\n");

            // ===== STAGE 2: Test Generation (parallel if Both) =====
            PrintStageHeader(2, $"Test Generation (strategy: {testPlan.Strategy})");

            TestSuite? unitTests = null;
            TestSuite? integrationTests = null;

            if (testPlan.Strategy == "Unit")
            {
                unitTests = await unitAgent.GenerateTestsAsync(testPlan, codeUnderTest);
                System.Console.WriteLine($"  Generated unit tests: {unitTests.TestFilePath} ({CountLines(unitTests.TestCode)} lines)");
            }
            else if (testPlan.Strategy == "Both")
            {
                System.Console.WriteLine("  Generating unit and integration tests in parallel...");
                var unitTask = unitAgent.GenerateTestsAsync(testPlan, codeUnderTest);
                var integTask = integrationAgent.GenerateTestsAsync(testPlan, codeUnderTest);
                await Task.WhenAll(unitTask, integTask);

                unitTests = unitTask.Result;
                integrationTests = integTask.Result;

                System.Console.WriteLine($"  Generated unit tests: {unitTests.TestFilePath} ({CountLines(unitTests.TestCode)} lines)");
                System.Console.WriteLine($"  Generated integration tests: {integrationTests.TestFilePath} ({CountLines(integrationTests.TestCode)} lines)");
            }

            workflowResult.TotalTestsGenerated = 1 + (integrationTests != null ? 1 : 0);
            System.Console.WriteLine();

            // ===== STAGE 3: Merge =====
            PrintStageHeader(3, "Test Suite Merge");

            var mergedCode = MergeTestSuites(unitTests, integrationTests, codeUnderTest.ClassName);
            System.Console.WriteLine($"  Merged test suite: {CountLines(mergedCode)} lines total\n");

            // ===== STAGE 4: Validation Loop =====
            PrintStageHeader(4, $"Build & Validation Loop (max {maxRetries} retries, runtime threshold {runtimeThreshold}s)");

            TestProjectScaffolder.ScaffoldResult? scaffold = null;
            bool validationPassed = false;
            string testStrategy = integrationTests != null ? "Both" : "Unit";

            for (int retry = 1; retry <= maxRetries; retry++)
            {
                System.Console.WriteLine($"  --- Attempt {retry}/{maxRetries} ---");

                if (scaffold == null)
                {
                    scaffold = await scaffolder.ScaffoldAsync(
                        codeUnderTest.SourceCode, codeUnderTest.ClassName,
                        mergedCode, testStrategy);
                }
                else
                {
                    await scaffolder.UpdateTestCode(scaffold, mergedCode, codeUnderTest.ClassName);
                }

                var artifact = new IterationArtifact
                {
                    Iteration = retry,
                    UnitTestCode = unitTests?.TestCode ?? string.Empty,
                    IntegrationTestCode = integrationTests?.TestCode ?? string.Empty,
                    MergedTestCode = mergedCode,
                    BuildSucceeded = scaffold.BuildSucceeded,
                    TestsPass = scaffold.TestsPass,
                    AnalyzerWarnings = scaffold.AnalyzerWarnings,
                    TestRunDuration = scaffold.TestRunDuration,
                    ErrorOutput = scaffold.BuildSucceeded ? scaffold.TestOutput : scaffold.BuildOutput
                };
                workflowResult.Artifacts.Add(artifact);
                workflowResult.ValidationRetries = retry;

                System.Console.WriteLine($"  Build: {(scaffold.BuildSucceeded ? "OK" : "FAILED")}");
                if (scaffold.BuildSucceeded)
                {
                    System.Console.WriteLine($"  Tests: {(scaffold.TestsPass ? "PASS" : "FAIL")} ({scaffold.TestsPassed} passed, {scaffold.TestsFailed} failed)");
                    System.Console.WriteLine($"  Analyzer warnings: {scaffold.AnalyzerWarnings}");
                    System.Console.WriteLine($"  Test runtime: {scaffold.TestRunDuration.TotalSeconds:F1}s");
                }

                if (!scaffold.BuildSucceeded)
                {
                    System.Console.WriteLine($"  Compilation errors detected. Sending to improvement agent...\n");
                    mergedCode = await improvementAgent.FixTestsAsync(
                        mergedCode, codeUnderTest, testPlan, scaffold.BuildOutput, isBuildError: true);
                    continue;
                }

                if (!scaffold.TestsPass)
                {
                    System.Console.WriteLine($"  Test failures detected. Sending to improvement agent...\n");
                    mergedCode = await improvementAgent.FixTestsAsync(
                        mergedCode, codeUnderTest, testPlan, scaffold.TestOutput, isBuildError: false);
                    continue;
                }

                if (scaffold.TestRunDuration.TotalSeconds > runtimeThreshold)
                {
                    System.Console.WriteLine($"  Runtime threshold exceeded ({scaffold.TestRunDuration.TotalSeconds:F1}s > {runtimeThreshold}s). Sending to improvement agent...\n");
                    mergedCode = await improvementAgent.FixTestsAsync(
                        mergedCode, codeUnderTest, testPlan,
                        $"Test runtime exceeded threshold: {scaffold.TestRunDuration.TotalSeconds:F1}s > {runtimeThreshold}s. Simplify or remove slow tests.",
                        isBuildError: false);
                    continue;
                }

                System.Console.WriteLine($"  Validation PASSED.\n");
                validationPassed = true;
                break;
            }

            if (!validationPassed)
            {
                System.Console.WriteLine($"  Validation failed after {maxRetries} retries. Proceeding to mutation testing with best available suite.\n");
            }

            // ===== STAGE 5: Stryker Mutation Testing (FINAL, no loop) =====
            PrintStageHeader(5, "Mutation Testing (final measurement)");

            if (scaffold != null && scaffold.BuildSucceeded)
            {
                var report = await mutationAgent.RunAnalysisAsync(scaffold.TestProjectPath, scaffold.SourceProjectPath);
                workflowResult.FinalMutationScore = report.MutationScore;

                PrintMutationReport(report);

                if (report.MutationScore >= targetScore)
                    System.Console.WriteLine($"  Target mutation score ({targetScore}%) reached!\n");
                else
                    System.Console.WriteLine($"  Below target ({report.MutationScore}% < {targetScore}%). See future work for iterative mutation improvement.\n");
            }
            else
            {
                System.Console.WriteLine("  Skipping mutation testing — tests did not pass validation.\n");
            }

            // ===== STAGE 6: Final Report =====
            stopwatch.Stop();
            workflowResult.TotalDuration = stopwatch.Elapsed;
            PrintFinalReport(workflowResult);

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"{codeUnderTest.ClassName}Tests.cs");
            await File.WriteAllTextAsync(outputPath, mergedCode);
            System.Console.WriteLine($"Final test file saved to: {outputPath}");

            var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), $"{codeUnderTest.ClassName}_artifacts.json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(artifactsPath, JsonSerializer.Serialize(workflowResult, jsonOptions));
            System.Console.WriteLine($"Artifacts saved to: {artifactsPath}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nError: {ex.Message}");
            System.Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }

    private static string MergeTestSuites(TestSuite? unitTests, TestSuite? integrationTests, string className)
    {
        if (unitTests != null && integrationTests == null)
            return unitTests.TestCode;

        if (unitTests == null && integrationTests != null)
            return integrationTests.TestCode;

        if (unitTests == null && integrationTests == null)
            return "// No tests generated";

        return $@"{unitTests!.TestCode}

// =============================================================================
// Integration (Component) Tests
// =============================================================================

{integrationTests!.TestCode}";
    }

    private static async Task<CodeUnderTest> LoadCodeUnderTestAsync(IConfiguration config)
    {
        var sourceFilePath = config["CodeUnderTest:SourceFile"];
        if (!string.IsNullOrWhiteSpace(sourceFilePath))
            sourceFilePath = Path.IsPathRooted(sourceFilePath)
                ? sourceFilePath
                : Path.Combine(Directory.GetCurrentDirectory(), sourceFilePath.Trim());

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            var baseDir = Directory.GetCurrentDirectory();
            var fallback1 = Path.Combine(baseDir, "..", "MutationAgentWorkflow.Sample", "PasswordValidator.cs");
            var fallback2 = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MutationAgentWorkflow.Sample", "PasswordValidator.cs");
            if (File.Exists(fallback1))
                sourceFilePath = Path.GetFullPath(fallback1);
            else if (File.Exists(fallback2))
                sourceFilePath = Path.GetFullPath(fallback2);
            else
                throw new FileNotFoundException(
                    "Code-under-test source file not found. Set CodeUnderTest:SourceFile in appsettings.json.");
        }

        var sourceCode = await File.ReadAllTextAsync(sourceFilePath);
        var className = config["CodeUnderTest:ClassName"];
        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(sourceFilePath);

        System.Console.WriteLine($"Source file: {sourceFilePath}");
        System.Console.WriteLine($"Class name:  {className}\n");

        return new CodeUnderTest
        {
            SourceCode = sourceCode,
            ClassName = className,
            FilePath = Path.GetFileName(sourceFilePath)
        };
    }

    private static void PrintStageHeader(int stage, string name)
    {
        System.Console.WriteLine($"[STAGE {stage}] {name}");
        System.Console.WriteLine(new string('-', 60));
    }

    private static void PrintMetrics(TestPlan plan)
    {
        var m = plan.Metrics;
        if (m is null) return;

        System.Console.WriteLine($"  Strategy:             {plan.Strategy}");
        System.Console.WriteLine($"  Cyclomatic complexity: {m.CyclomaticComplexity} (total)");
        if (m.MethodComplexities.Count > 0)
        {
            foreach (var kv in m.MethodComplexities.OrderByDescending(kv => kv.Value))
                System.Console.WriteLine($"    {kv.Key}: {kv.Value}");
        }
        System.Console.WriteLine($"  Dependencies:          {m.DependencyCount} ({(m.InjectedDependencies.Count > 0 ? string.Join(", ", m.InjectedDependencies) : "none")})");
        System.Console.WriteLine($"  Controller/endpoint:   {m.IsControllerOrEndpoint}");

        if (m.LongMethods.Count > 0)
            System.Console.WriteLine($"  Long methods (>30 lines): {string.Join(", ", m.LongMethods)}");
        if (m.HighParamMethods.Count > 0)
            System.Console.WriteLine($"  High-param methods:    {string.Join(", ", m.HighParamMethods)}");
        if (m.MaxNestingDepth > 0)
            System.Console.WriteLine($"  Max nesting depth:     {m.MaxNestingDepth}");

        System.Console.WriteLine($"  Reasoning:             {m.Reasoning}\n");
    }

    private static void PrintMutationReport(MutationReport report)
    {
        System.Console.WriteLine($"  Mutation Score: {report.MutationScore}%");
        System.Console.WriteLine($"  Total: {report.TotalMutants} | Killed: {report.KilledMutants} | Survived: {report.SurvivedMutants}");

        foreach (var mutant in report.SurvivedMutantDetails.Take(10))
        {
            System.Console.WriteLine($"    - [{mutant.MutationType}] {mutant.Location}");
            if (!string.IsNullOrWhiteSpace(mutant.OriginalCode))
                System.Console.WriteLine($"      '{mutant.OriginalCode}' -> '{mutant.MutatedCode}'");
        }

        if (report.SurvivedMutantDetails.Count > 10)
            System.Console.WriteLine($"    ... and {report.SurvivedMutantDetails.Count - 10} more survived mutants.");

        System.Console.WriteLine();
    }

    private static void PrintFinalReport(WorkflowResult result)
    {
        System.Console.WriteLine("\n=== WORKFLOW SUMMARY ===");
        System.Console.WriteLine($"  Final Mutation Score:   {result.FinalMutationScore}%");
        System.Console.WriteLine($"  Validation Retries:     {result.ValidationRetries}");
        System.Console.WriteLine($"  Tests Generated:        {result.TotalTestsGenerated}");
        System.Console.WriteLine($"  Total Duration:         {result.TotalDuration.TotalSeconds:F1}s");

        if (result.Artifacts.Count > 0)
        {
            System.Console.WriteLine("  --- Artifact History ---");
            foreach (var a in result.Artifacts)
            {
                System.Console.WriteLine($"    Attempt {a.Iteration}: build={a.BuildSucceeded}, tests={a.TestsPass}, warnings={a.AnalyzerWarnings}, runtime={a.TestRunDuration.TotalSeconds:F1}s");
            }
        }

        System.Console.WriteLine(new string('=', 40) + "\n");
    }

    private static string Indent(string text, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join("\n", text.Split('\n').Select(line => prefix + line));
    }

    private static int CountLines(string text) => text.Split('\n').Length;
}
