using System.Diagnostics;
using System.Text.Json;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Tools;

public class StrykerRunner
{
    public async Task<MutationReport> RunMutationTestingAsync(string testProjectPath, string sourceProjectPath)
    {
        try
        {
            var testProjectDir = Path.GetDirectoryName(testProjectPath)!;
            var sourceProjectName = Path.GetFileNameWithoutExtension(sourceProjectPath);
            var outputDir = Path.Combine(testProjectDir, "StrykerOutput");

            var strykerArgs = $"--project {sourceProjectName}.csproj --reporter json --reporter cleartext --output \"{outputDir}\"";

            Console.WriteLine($"  Running Stryker in: {testProjectDir}");

            var (exitCode, output, error) = await RunCommandAsync("dotnet", $"stryker {strykerArgs}", testProjectDir);

            if (exitCode != 0 && error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  'dotnet stryker' not found, trying 'dotnet-stryker'...");
                (exitCode, output, error) = await RunCommandAsync("dotnet-stryker", strykerArgs, testProjectDir);
            }

            if (exitCode != 0)
            {
                Console.WriteLine($"  Stryker exited with code {exitCode}.");
                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine($"  stderr (truncated): {error[..Math.Min(error.Length, 300)]}");
            }

            var reportPath = FindStrykerReport(testProjectDir);
            if (reportPath is not null)
            {
                Console.WriteLine($"  Found Stryker report: {reportPath}");
                return await ParseStrykerJsonAsync(reportPath);
            }

            Console.WriteLine("  Stryker JSON report not found. Attempting to parse cleartext output.");
            return ParseCleartextFallback(output);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Stryker error: {ex.Message}");
            Console.WriteLine("  Make sure Stryker.NET is installed: dotnet tool install -g dotnet-stryker");
            return new MutationReport { MutationScore = 0 };
        }
    }

    private static string? FindStrykerReport(string testProjectDir)
    {
        var strykerOutputDir = Path.Combine(testProjectDir, "StrykerOutput");
        if (!Directory.Exists(strykerOutputDir))
            return null;

        return Directory.GetFiles(strykerOutputDir, "mutation-report.json", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static async Task<MutationReport> ParseStrykerJsonAsync(string reportPath)
    {
        var json = await File.ReadAllTextAsync(reportPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var report = new MutationReport();

        if (!root.TryGetProperty("files", out var files))
            return report;

        foreach (var file in files.EnumerateObject())
        {
            if (!file.Value.TryGetProperty("mutants", out var mutants))
                continue;

            foreach (var mutant in mutants.EnumerateArray())
            {
                report.TotalMutants++;

                var status = mutant.GetProperty("status").GetString() ?? "";

                if (status.Equals("Killed", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
                {
                    report.KilledMutants++;
                }
                else if (status.Equals("Survived", StringComparison.OrdinalIgnoreCase) ||
                         status.Equals("NoCoverage", StringComparison.OrdinalIgnoreCase))
                {
                    report.SurvivedMutants++;

                    var location = "";
                    if (mutant.TryGetProperty("location", out var loc))
                    {
                        var startLine = loc.TryGetProperty("start", out var start) && start.TryGetProperty("line", out var sl) ? sl.GetInt32().ToString() : "?";
                        location = $"Line {startLine} in {file.Name}";
                    }

                    report.SurvivedMutantDetails.Add(new SurvivedMutant
                    {
                        MutationType = mutant.TryGetProperty("mutatorName", out var mn) ? mn.GetString() ?? "Unknown" : "Unknown",
                        Location = location,
                        OriginalCode = mutant.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        MutatedCode = mutant.TryGetProperty("replacement", out var rep) ? rep.GetString() ?? "" : ""
                    });
                }
            }
        }

        report.MutationScore = report.TotalMutants > 0
            ? Math.Round((double)report.KilledMutants / report.TotalMutants * 100, 2)
            : 0;

        return report;
    }

    private static MutationReport ParseCleartextFallback(string output)
    {
        var report = new MutationReport();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("mutation score", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(':');
                if (parts.Length >= 2)
                {
                    var scoreStr = parts[^1].Trim().TrimEnd('%').Trim();
                    if (double.TryParse(scoreStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var score))
                        report.MutationScore = score;
                }
            }
        }
        return report;
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
