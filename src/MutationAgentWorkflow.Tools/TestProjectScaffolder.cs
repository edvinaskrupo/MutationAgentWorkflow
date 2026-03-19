using System.Diagnostics;

namespace MutationAgentWorkflow.Tools;

public class TestProjectScaffolder
{
    public class ScaffoldResult
    {
        public string SolutionDir { get; set; } = string.Empty;
        public string SourceProjectPath { get; set; } = string.Empty;
        public string TestProjectPath { get; set; } = string.Empty;
        public string TestFilePath { get; set; } = string.Empty;
        public bool BuildSucceeded { get; set; }
        public string BuildOutput { get; set; } = string.Empty;
    }

    public async Task<ScaffoldResult> ScaffoldAsync(
        string sourceCode,
        string className,
        string testCode,
        string testStrategy)
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "MutationWorkflow", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(baseDir);

        var sourceProjectDir = Path.Combine(baseDir, "SourceProject");
        var testProjectDir = Path.Combine(baseDir, "TestProject");
        Directory.CreateDirectory(sourceProjectDir);
        Directory.CreateDirectory(testProjectDir);

        await WriteSourceProject(sourceProjectDir, sourceCode, className);
        await WriteTestProject(testProjectDir, sourceProjectDir, testCode, className, testStrategy);
        await WriteSolution(baseDir, sourceProjectDir, testProjectDir);

        var result = new ScaffoldResult
        {
            SolutionDir = baseDir,
            SourceProjectPath = Path.Combine(sourceProjectDir, "SourceProject.csproj"),
            TestProjectPath = Path.Combine(testProjectDir, "TestProject.csproj"),
            TestFilePath = Path.Combine(testProjectDir, $"{className}Tests.cs")
        };

        var (exitCode, output, _) = await RunCommandAsync("dotnet", "build", baseDir);
        result.BuildSucceeded = exitCode == 0;
        result.BuildOutput = output;

        return result;
    }

    public async Task UpdateTestCode(ScaffoldResult scaffold, string newTestCode, string className)
    {
        var testFilePath = Path.Combine(Path.GetDirectoryName(scaffold.TestProjectPath)!, $"{className}Tests.cs");
        await File.WriteAllTextAsync(testFilePath, newTestCode);
        scaffold.TestFilePath = testFilePath;

        var (exitCode, output, _) = await RunCommandAsync("dotnet", "build", scaffold.SolutionDir);
        scaffold.BuildSucceeded = exitCode == 0;
        scaffold.BuildOutput = output;
    }

    private static async Task WriteSourceProject(string dir, string sourceCode, string className)
    {
        var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(dir, "SourceProject.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(dir, $"{className}.cs"), sourceCode);
    }

    private static async Task WriteTestProject(string dir, string sourceProjectDir, string testCode, string className, string testStrategy)
    {
        var moqReference = testStrategy == "Integration"
            ? @"    <PackageReference Include=""Moq"" Version=""4.20.72"" />"
            : "";

        var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.12.0"" />
    <PackageReference Include=""xunit"" Version=""2.9.3"" />
    <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.8.2"" />
{moqReference}
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""{Path.Combine(sourceProjectDir, "SourceProject.csproj")}"" />
  </ItemGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(dir, "TestProject.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(dir, $"{className}Tests.cs"), testCode);
    }

    private static async Task WriteSolution(string baseDir, string sourceDir, string testDir)
    {
        await RunCommandAsync("dotnet", "new sln -n Workspace", baseDir);
        await RunCommandAsync("dotnet", $"sln add \"{Path.Combine(sourceDir, "SourceProject.csproj")}\"", baseDir);
        await RunCommandAsync("dotnet", $"sln add \"{Path.Combine(testDir, "TestProject.csproj")}\"", baseDir);
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
