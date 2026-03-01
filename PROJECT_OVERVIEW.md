# Project Overview: Mutation-Guided Agentic Test Generation Workflow

**Document purpose:** Committee-facing description of the Bachelor's thesis prototype: structure, framework, logic, usage, and testing.

---

## 1. What This Project Does (One Paragraph)

The prototype is an **agent-based system** that helps generate and improve **unit or integration tests** for C# code. The user provides a class or service; the system suggests a test strategy (unit vs integration), generates xUnit test code, simulates mutation-testing results, and—when mutants survive—suggests concrete improvements to strengthen the tests. The goal is to demonstrate how **multiple AI agents** can work in a pipeline to support **robustness in testing processes**, with a path toward real mutation testing (e.g. Stryker.NET) and higher mutation scores.

---

## 2. High-Level Architecture

The system is a **four-stage pipeline** with one human decision in the middle:

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  STAGE 1        │     │  STAGE 2        │     │  STAGE 3        │     │  STAGE 4        │
│  Test Planning  │ ──► │  Test Generation│ ──► │  Mutation       │ ──► │  Test           │
│  Agent          │     │  Agent          │     │  Analysis       │     │  Improvement    │
│                 │     │                 │     │  Agent          │     │  Agent          │
└────────┬────────┘     └─────────────────┘     └────────┬────────┘     └─────────────────┘
         │                                                         │
         │  User chooses: 1 = Unit, 2 = Integration                │  Only if survived
         ▼                                                         ▼  mutants > 0
    [Human choice]                                            [Suggestions]
```

- **Stage 1:** An AI agent analyzes the code and suggests a test strategy (unit vs integration, what to mock, which methods to test first).
- **Stage 2:** The user selects “Unit” or “Integration”; a second agent generates xUnit test code from that plan and the source code.
- **Stage 3:** Mutation analysis runs (currently **simulated**; in a full version this would run Stryker.NET and compute a real mutation score).
- **Stage 4:** If any mutants survived, a third AI agent suggests specific test improvements to kill those mutants and raise the mutation score.

---

## 3. Technology and Framework

| Item | Choice |
|------|--------|
| **Language** | C# |
| **Runtime** | .NET 9 |
| **AI/LLM** | Microsoft Semantic Kernel 1.71, with OpenAI chat completion (e.g. gpt-4o, gpt-4o-mini) |
| **Config** | JSON (`appsettings.json`: API key, model name; optionally `CodeUnderTest:SourceFile`, `CodeUnderTest:ClassName`) |
| **Test framework (generated code)** | xUnit |

The project does **not** use Python or a separate ML framework; all logic is in .NET. Semantic Kernel is used only to send prompts to the OpenAI API and receive text responses.

---

## 4. Solution and Code Structure

The repository has **one solution** at the root: `BachelorProject.sln`. It contains **five projects**:

| Project | Role | Main contents |
|---------|------|----------------|
| **MutationAgentWorkflow.Core** | Shared data and no external deps | Models: `CodeUnderTest`, `TestPlan`, `TestSuite`, `MutationReport`, `WorkflowResult` |
| **MutationAgentWorkflow.Agents** | AI agents | `TestPlanningAgent`, `TestGenerationAgent`, `MutationAnalysisAgent`, `TestImprovementAgent` (uses Semantic Kernel + Core) |
| **MutationAgentWorkflow.Tools** | Optional tooling for future use | `StrykerRunner`, `DotNetTestRunner` (not used in the current single-pass run) |
| **MutationAgentWorkflow.Console** | Entry point and workflow orchestration | `Program.cs`, `appsettings.json`; references Core, Agents, Tools |
| **MutationAgentWorkflow.Sample** | Code under test (real class) | `Calculator.cs`; Console loads its source from disk by default |

Dependency flow:

- **Console** → Agents, Tools, Core  
- **Agents** → Core  
- **Tools** → Core  
- **Core** → (none)

Folder layout (simplified):

```
BachelorProject/
├── BachelorProject.sln                    ← Open this to build/run
└── MutationAgentWorkflow/
    ├── README.md
    ├── PROJECT_OVERVIEW.md                 ← This document
    ├── MutationAgentWorkflow.sln           ← Alternative: build from here
    └── src/
        ├── MutationAgentWorkflow.Core/
        │   └── Models/                     ← CodeUnderTest, TestPlan, TestSuite, MutationReport, WorkflowResult
        ├── MutationAgentWorkflow.Agents/
        │   └── *.cs                        ← Four agent classes
        ├── MutationAgentWorkflow.Tools/
        │   └── *.cs                        ← StrykerRunner, DotNetTestRunner
        ├── MutationAgentWorkflow.Sample/
        │   └── Calculator.cs               ← Real class under test (Console loads this by default)
        └── MutationAgentWorkflow.Console/
            ├── Program.cs                  ← Workflow: load config, run 4 stages, print summary
            └── appsettings.json            ← OpenAI:ApiKey, OpenAI:Model
```

---

## 5. Main Components in Plain Language

### 5.1 Core Models (Data Only)

- **CodeUnderTest** – The piece of code to test: source text, class name, file path (and optionally method list).
- **TestPlan** – Strategy (Unit/Integration), free-text suggestion from the planning agent, and lists such as methods to test and dependencies to mock.
- **TestSuite** – Generated test code (string), output file path, and optional run results (compile/tests pass, etc.).
- **MutationReport** – Mutation score (%), total/killed/survived mutant counts, and a list of survived mutants (type, location, original vs mutated code).
- **WorkflowResult** – Summary of a run: initial/final mutation score, iterations, number of tests generated, duration.

### 5.2 Agents

- **TestPlanningAgent** – Uses the LLM to analyze C# code and return a test strategy (unit vs integration, mocks, priority methods, reasoning). The workflow parses the text to set `TestPlan.Strategy` and `TestPlan.Suggestion`.
- **TestGenerationAgent** – Takes the test plan and code, sends a prompt to the LLM, and returns a `TestSuite` with generated xUnit test code and a suggested file name (e.g. `CalculatorTests.cs`).
- **MutationAnalysisAgent** – In the prototype it **does not call the LLM**. It returns a **mock** `MutationReport` (e.g. 75% score, 20 mutants, 5 survived, one example survived mutant). In a full version it would call StrykerRunner and parse Stryker’s JSON report.
- **TestImprovementAgent** – Takes the mutation report and current test suite, asks the LLM for concrete improvements (assertions, missing cases, code changes) to kill survived mutants.

### 5.3 Tools (Present but Not Used in Current Run)

- **StrykerRunner** – Intended to run `dotnet stryker` and parse the mutation report JSON; currently the workflow uses only the mock from MutationAnalysisAgent.
- **DotNetTestRunner** – Intended to build and run tests for a project path; reserved for future use (e.g. compile generated tests, run them, then run Stryker).

### 5.4 Console (Program.cs) – Workflow Logic

1. Load `appsettings.json` (OpenAI API key and model; optionally `CodeUnderTest:SourceFile` and `CodeUnderTest:ClassName`).
2. Load **CodeUnderTest** from a real class file: resolve path from config or use default `MutationAgentWorkflow.Sample/Calculator.cs`, read source with `File.ReadAllTextAsync`, derive class name from file name if not in config.
3. **Stage 1:** Call `TestPlanningAgent.GeneratePlanAsync` → print suggestion → ask user to choose 1 (Unit) or 2 (Integration) → set `TestPlan.Strategy`.
4. **Stage 2:** Call `TestGenerationAgent.GenerateTestsAsync` → print generated code → save to a file (e.g. `CalculatorTests.cs` in the current directory).
5. **Stage 3:** Call `MutationAnalysisAgent.ParseStrykerReport("")` to get the **mock** mutation report → print score and survived mutants.
6. **Stage 4:** If there are survived mutants, call `TestImprovementAgent.SuggestImprovementsAsync` → print improvement suggestions.
7. Print a **workflow summary** (mutation score, tests generated, duration).

No iterative loop: one pass only. No real Stryker or test execution in the default run.

---

## 6. How to Use the Prototype

### 6.1 Prerequisites

- **.NET 9 SDK** installed ([download](https://dotnet.microsoft.com/download/dotnet/9.0)).
- **OpenAI API key** ([create one](https://platform.openai.com/api-keys)).

### 6.2 Setup

1. Open the solution: from the repo root, open `BachelorProject.sln` (or from `MutationAgentWorkflow` open `MutationAgentWorkflow.sln`).
2. Edit configuration:
   - Go to `MutationAgentWorkflow/src/MutationAgentWorkflow.Console/appsettings.json`.
   - Set `OpenAI:ApiKey` to your key (replace the placeholder).
   - Optionally set `OpenAI:Model` (e.g. `gpt-4o-mini` or `gpt-4o`).

### 6.3 Build

From the repo root:

```bash
dotnet build BachelorProject.sln
```

Or from the `MutationAgentWorkflow` folder:

```bash
dotnet build MutationAgentWorkflow.sln
```

### 6.4 Run

```bash
cd MutationAgentWorkflow/src/MutationAgentWorkflow.Console
dotnet run
```

Then:

1. Read the **Stage 1** output (test strategy suggestion).
2. When prompted, type **1** for Unit tests or **2** for Integration tests and press Enter.
3. Read the **Stage 2** output (generated test code); the app saves it to a file (e.g. `CalculatorTests.cs` in the current directory).
4. Read **Stage 3** (simulated mutation score and survived mutants).
5. If Stage 4 runs, read the improvement suggestions.
6. Read the final **workflow summary**.

---

## 7. How to Test / Verify the Prototype

- **Manual run (recommended for committee):** Follow section 6; confirm that all four stages run, the strategy choice is respected, test code is generated and saved, mock mutation data is shown, and (when applicable) improvement suggestions appear. Check the summary metrics (mutation score, duration, etc.).
- **Build verification:** From the repo root, run `dotnet build BachelorProject.sln`. Expect zero errors; the Console project is the executable.
- **Generated test file:** After a run, open the generated `*Tests.cs` file in the Console output directory; confirm it is valid C# xUnit-style code (namespace, class, `[Fact]`, assertions). It will not run automatically in this prototype (no test project is created or executed).
- **API key:** If the key is missing or invalid, the app throws when calling the first agent; that confirms the pipeline reaches the LLM.

There is no automated test suite (no unit/integration tests) for the prototype itself; the focus is on demonstrating the workflow and the agents’ behavior.

---

## 8. Current Limitations (Prototype)

- **Mutation testing is simulated** – No real Stryker run; mutation report is hardcoded in `MutationAnalysisAgent.ParseStrykerReport`.
- **Single pass** – No loop to re-generate tests from improvement suggestions until a target mutation score is reached.
- **Generated tests are not compiled or run** – The test file is written to disk but not added to a test project; `DotNetTestRunner` and `StrykerRunner` are not invoked.
- **Code under test** – Loaded from the real class `MutationAgentWorkflow.Sample/Calculator.cs` by default, or from a path set in `CodeUnderTest:SourceFile` in appsettings; generated tests are not yet wired to a test project that references Sample.

---

## 9. Possible Extensions (For Discussion)

- **Real Stryker integration:** Create a temporary test project, write generated tests to it, run `dotnet stryker`, parse the JSON report, and feed it into `MutationAnalysisAgent`.
- **Use DotNetTestRunner:** After generating tests, build and run the test project to verify compilation and green tests before/after mutation.
- **Iterative loop:** Re-run generation and mutation (and optionally test run) until a target mutation score or max iterations is reached.
- **File-based input:** Accept a path to a C# file or folder and load `CodeUnderTest` from disk instead of the hardcoded example.

---

## 10. Thesis Context in One Sentence

This prototype illustrates an **agentic workflow for robustness in testing**: multiple AI agents collaborate in a pipeline (plan → generate tests → mutation analysis → improvement suggestions) to support test quality and mutation score improvement, with a path to full automation and real mutation testing.

---

*Document version: for committee presentation. Aligns with README and current codebase.*
