# Mutation-Guided Agentic Test Generation Workflow

A Bachelor's thesis prototype that uses a pipeline of LLM-based agents to generate, validate and improve unit / integration tests for C# code, with quality measured by Stryker.NET mutation testing.

The repository contains only the experimental system; the thesis document (`.tex`, `.bib`) and related writing artifacts are kept outside this repository.

## What the system does

Given a C# source file, the system:

1. **Analyses the code** (cyclomatic complexity, dependencies, controller / endpoint heuristics) to choose between a *Unit*, *Integration* or *Skip* strategy.
2. **Drafts a test plan** with an LLM-based planning agent (methods to cover, mocks, scenarios).
3. **Generates an xUnit test class** using a strategy-specific generation agent (unit-only or integration-with-Moq).
4. **Scaffolds a real .NET test project**, compiles it, and runs the tests against the unmutated source.
5. **Repairs failing tests** in a validation loop: on build error, test failure, or excessive runtime, a repair agent is called with the relevant error output and produces a corrected test class. The loop continues up to a configurable retry budget.
6. **Runs Stryker.NET mutation analysis** on the validated test suite and reports the mutation score.

All per-run artefacts (final test class, structured JSON report) are written to `experiment_results/`.

## Architecture

```
   Source code
        |
        v
 [CodeMetricsAnalyzer] --> strategy: Unit | Integration | Skip
        |
        v
 [TestPlanningAgent] --> test plan
        |
        v
 [UnitTestGenerationAgent | IntegrationTestGenerationAgent]
        |
        v
 [TestProjectScaffolder] --> build + run tests
        |
        v
 build / test / runtime errors? --[yes]--> [TestImprovementAgent] --(loop, up to N retries)
        |
       [no]
        |
        v
 [MutationAnalysisAgent] --> Stryker.NET --> mutation score + JSON report
```

| Layer | Project | Role |
|-------|---------|------|
| Domain models | `MutationAgentWorkflow.Core` | `CodeUnderTest`, `TestPlan`, `TestSuite`, `MutationReport`, `WorkflowResult`, `IterationArtifact`, plus `CodeMetricsAnalyzer` (Roslyn). |
| LLM agents | `MutationAgentWorkflow.Agents` | `TestPlanningAgent`, `UnitTestGenerationAgent`, `IntegrationTestGenerationAgent`, `TestImprovementAgent`, `MutationAnalysisAgent`. |
| Runtime tools | `MutationAgentWorkflow.Tools` | `TestProjectScaffolder` (creates and updates the temporary test project), `DotNetTestRunner`, `StrykerRunner`. |
| Entry point | `MutationAgentWorkflow.Console` | `Program.cs` — loads config, runs the pipeline, writes artefacts. |
| Sample inputs | `MutationAgentWorkflow.Sample` | `PasswordValidator`, `UserService`, `OrderProcessor` — used as code under test in the experiments. |

## Prerequisites

- **.NET 9 SDK** — <https://dotnet.microsoft.com/download/dotnet/9.0>
- **OpenAI API key** — <https://platform.openai.com/api-keys>
- **Stryker.NET** (required for the mutation-analysis stage):
  ```bash
  dotnet tool install -g dotnet-stryker
  ```

## Setup

The Console loads `appsettings.json` first and then, if present, `appsettings.local.json` on top (the second file overrides values from the first). `appsettings.local.json` is gitignored, so it's the safe place to keep your real API key.

1. Create `src/MutationAgentWorkflow.Console/appsettings.local.json` with at least your key, e.g.:

   ```json
   {
     "OpenAI": {
       "ApiKey": "sk-..."
     }
   }
   ```

   The tracked `appsettings.json` ships with a `YOUR_OPENAI_API_KEY` placeholder — leave it as-is and **do not commit a real key in its place**.

2. Optional — override any other value in `appsettings.local.json`:
   - `OpenAI:Model` — e.g. `gpt-5.4-mini` (model used in the thesis experiments)
   - `Workflow:MaxRetries`, `Workflow:RuntimeThresholdSeconds`, `Workflow:TargetMutationScore`
   - `CodeUnderTest:SourceFile` — path to a `.cs` file (absolute, or relative to the Console working directory)
   - `CodeUnderTest:ClassName` — class name (defaults to the file name)

   If `CodeUnderTest:SourceFile` is omitted, the Console loads `MutationAgentWorkflow.Sample/PasswordValidator.cs` by default.

## Build and run

From the repository root:

```bash
dotnet build MutationAgentWorkflow.sln
cd src/MutationAgentWorkflow.Console
dotnet run
```

The Console prints each pipeline stage to standard output and writes the final test class plus a per-run JSON artefact under `experiment_results/`.

## Repository structure

```
MutationAgentWorkflow/
├── src/
│   ├── MutationAgentWorkflow.Core/      # Models + Roslyn-based code metrics
│   ├── MutationAgentWorkflow.Agents/    # LLM agents
│   ├── MutationAgentWorkflow.Tools/     # Scaffolder, Stryker / dotnet test runners
│   ├── MutationAgentWorkflow.Console/   # Entry point + appsettings.json
│   └── MutationAgentWorkflow.Sample/    # Sample classes used as code under test
├── experiment_results/                  # Generated tests + JSON reports per run
├── MutationAgentWorkflow.sln
├── README.md
└── .gitignore
```

## Experimental data

`experiment_results/` contains the artefacts referenced in the thesis evaluation: per-class test files (`*_Tests.cs`) and structured JSON reports (`*_artifacts.json`) for `PasswordValidator`, `UserService` and `OrderProcessor`, three runs each. The files are named `<Class>_run<N>_*.{cs,json}` so that any single run can be inspected end-to-end (final test class plus full iteration history and Stryker result inside the JSON).

## License

MIT — see `LICENSE` if present, or the licensing notes in the accompanying thesis document.
