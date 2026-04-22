using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MutationAgentWorkflow.Tools;

public class TestRunResult
{
    public bool AllTestsPass { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Total => Passed + Failed + Skipped;
    public string Output { get; set; } = string.Empty;
}

public class DotNetTestRunner
{
    public async Task<TestRunResult> RunTestsAsync(string solutionOrProjectDir)
    {
        var result = new TestRunResult();

        try
        {
            var (exitCode, output, error) = await RunCommandAsync("dotnet", "test --no-build --verbosity normal", solutionOrProjectDir);
            result.Output = output + error;

            ParseTestCounts(result, output);

            result.AllTestsPass = exitCode == 0 && result.Failed == 0;
        }
        catch (Exception ex)
        {
            result.Output = ex.Message;
            result.AllTestsPass = false;
        }

        return result;
    }

    private static void ParseTestCounts(TestRunResult result, string output)
    {
        var passedMatch = Regex.Match(output, @"Passed:\s*(\d+)", RegexOptions.IgnoreCase);
        if (passedMatch.Success)
            result.Passed = int.Parse(passedMatch.Groups[1].Value);

        var failedMatch = Regex.Match(output, @"Failed:\s*(\d+)", RegexOptions.IgnoreCase);
        if (failedMatch.Success)
            result.Failed = int.Parse(failedMatch.Groups[1].Value);

        var skippedMatch = Regex.Match(output, @"Skipped:\s*(\d+)", RegexOptions.IgnoreCase);
        if (skippedMatch.Success)
            result.Skipped = int.Parse(skippedMatch.Groups[1].Value);

        if (result.Passed == 0 && result.Failed == 0)
        {
            var totalMatch = Regex.Match(output, @"Total tests:\s*(\d+)", RegexOptions.IgnoreCase);
            if (totalMatch.Success)
            {
                var total = int.Parse(totalMatch.Groups[1].Value);
                if (output.Contains("Passed!", StringComparison.OrdinalIgnoreCase))
                    result.Passed = total;
            }
        }
    }

    private static async Task<(int exitCode, string output, string error)> RunCommandAsync(
        string fileName, string arguments, string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }
}
