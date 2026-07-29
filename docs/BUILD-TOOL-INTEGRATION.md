# Build Tool Integration

`benchmark-gate` is a standard `dotnet tool` — installing and invoking it
from NUKE or Cake works today via a normal local tool manifest, with no
BenchmarkGate-specific package required. This doc shows all three common
paths. Typed wrappers (a NUKE component, a Cake addin) are a planned
later convenience — see `ROADMAP.md` — not a prerequisite for using the
tool from either build system right now.

## 1. Install as a local tool (shared by all three approaches below)

From your repo root:

```bash
dotnet new tool-manifest    # only if you don't already have one
dotnet tool install Bijecta.BenchmarkGate.Tool --version 0.2.0-alpha.1
```

This creates/updates `.config/dotnet-tools.json`, committed to the repo,
so `dotnet tool restore` reproduces the exact same version on any machine
or CI runner — no global install required anywhere.

```bash
dotnet tool restore
dotnet tool run benchmark-gate -- check --results ... --baseline ... --policy ...
```

## 2. Plain shell / CI YAML

The simplest integration — no build-tool-specific wiring at all:

```bash
dotnet tool restore
dotnet benchmark-gate check \
  --results ./BenchmarkDotNet.Artifacts/results \
  --baseline ./benchmarks/baseline.json \
  --policy ./benchmarks/policy.json \
  --junit ./benchmarkgate-junit.xml \
  --fail-on-warning
```

(`dotnet benchmark-gate ...` works directly once the tool is restored,
without the `tool run` prefix, since it registers a `dotnet` command.)

In GitHub Actions:

```yaml
- name: Restore tools
  run: dotnet tool restore

- name: Run benchmarks
  run: dotnet run -c Release --project ./bench/MyProject.Benchmarks

- name: Gate performance
  run: >
    dotnet benchmark-gate check
    --results ./BenchmarkDotNet.Artifacts/results
    --baseline ./benchmarks/baseline.json
    --policy ./benchmarks/policy.json
    --junit ./benchmarkgate-junit.xml
```

## 3. NUKE

NUKE can invoke any restored local tool via the generic process-invocation
helpers. Until a typed `Nuke.Common.Tools.BenchmarkGate` wrapper exists
(see `ROADMAP.md`), use `ProcessTasks` directly:

```csharp
using Nuke.Common;
using Nuke.Common.Tooling;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    Target GatePerformance => _ => _
        .DependsOn(RunBenchmarks)
        .Executes(() =>
        {
            DotNet(
                $"benchmark-gate check " +
                $"--results {BenchmarkResultsDirectory} " +
                $"--baseline {RootDirectory / "benchmarks" / "baseline.json"} " +
                $"--policy {RootDirectory / "benchmarks" / "policy.json"} " +
                $"--junit {ArtifactsDirectory / "benchmarkgate-junit.xml"}");
        });
}
```

`DotNet(...)` (from `Nuke.Common.Tools.DotNet`) shells out through the
`dotnet` CLI, which resolves the local tool the same way the plain-shell
example does. NUKE surfaces the process exit code as a build failure
automatically — a non-zero `benchmark-gate check` exit code fails the
NUKE target, no extra wiring needed.

## 4. Cake

Cake's `DotNetTool` alias (from the `Cake.Common.Tools.DotNet` namespace,
built into Cake core — no extra addin needed for this path) invokes any
restored local tool:

```csharp
Task("GatePerformance")
    .IsDependentOn("RunBenchmarks")
    .Does(() =>
{
    DotNetTool(".", "benchmark-gate", new ProcessArgumentBuilder()
        .Append("check")
        .Append("--results").Append(benchmarkResultsDirectory)
        .Append("--baseline").Append(baselinePath)
        .Append("--policy").Append(policyPath)
        .Append("--junit").Append(junitOutputPath));
});
```

Same exit-code behavior as NUKE: `DotNetTool` throws on a non-zero exit
code by default, which fails the Cake task — matching how `benchmark-gate
check` is meant to be consumed as a real gate, not just a report step.

## Planned: typed wrappers

Both of the above work but require hand-writing the CLI argument string.
Planned, not yet built (see `ROADMAP.md`):

- A generated NUKE component (`Nuke.Common.Tools.BenchmarkGate`) giving
  typed settings objects and IntelliSense instead of string
  concatenation.
- A Cake addin (`Cake.BenchmarkGate`) with alias methods like
  `BenchmarkGateCheck(settings => settings.SetPolicy(...))`.

If you're using one of these today and want to help shape that API,
open an issue — real usage patterns are more useful input than a
design guess.
