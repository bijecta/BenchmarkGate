# Exit codes

BenchmarkGate's CLI commands communicate outcome through the process exit
code, so CI systems can gate on `$?`/`$LASTEXITCODE` without parsing
console output. These meanings are part of BenchmarkGate's public CLI
contract. After the stable 1.0.0 release, exit-code meanings will not
change without a major version bump.

| Code | Name | Meaning | Returned by |
|---|---|---|---|
| 0 | `Passed` | The command completed successfully with no failures. | `check`, `capture`, `validate` |
| 1 | `Regressed` | At least one benchmark regressed past its policy's failure threshold. | `check` |
| 2 | `InvalidArguments` | The command's arguments were invalid or incomplete — e.g. an empty `--suite`, an output path that already exists without `--overwrite`, or `validate` invoked with none of `--policy`/`--baseline`/`--results`. | `check`, `capture`, `validate` |
| 3 | `InvalidBaselineOrPolicy` | The baseline or policy file failed to load — missing, malformed, or fails semantic validation. | `check` |
| 4 | `IncompleteResultSet` | The baseline contains a benchmark not present in the current results (`Missing` status). | `check` |
| 5 | `IncompatibleEnvironment` | Reserved for future functionality — an environment-compatibility evaluator (deferred past v0.1, see ROADMAP.md). Verified: not currently returned by `SuiteDecision.GetExitCode` or any command. | — |
| 6 | `UnstableResults` | At least one benchmark failed the stability gate (too few measurements, or coefficient of variation above the policy's threshold). | `check` |
| 7 | `UnapprovedNewBenchmarks` | Reserved for future functionality — a `--reject-new`-style gate on benchmarks with no baseline entry. Verified: not currently returned by `SuiteDecision.GetExitCode` or any command. | — |
| 8 | `UnsupportedSchema` | The BenchmarkDotNet results file/directory failed to parse — missing, malformed, unsupported schema, or (for `capture`) parsed to zero observations. | `check`, `capture` |
| 9 | `Warning` | A Warning-only suite (no Regressed, Missing, or Unstable benchmarks) evaluated with `--fail-on-warning`. | `check` |
| 10 | `InternalError` | An unexpected, unhandled exception reached the process boundary — a bug, not an expected failure mode. See `Program.cs`'s top-level catch. | any command |
| 11 | `OutputWriteFailure` | A requested report (Markdown/JSON/JUnit) or baseline candidate could not be written to disk. | `check`, `capture`, `validate` |
| 12 | `ValidationFailed` | At least one artifact requested via `validate` has error-level diagnostics, or could not be read/parsed at all. Used uniformly regardless of whether the artifact was a policy, baseline, or results file, and regardless of whether the cause was semantic (a validation rule failed) or syntactic (malformed JSON) — `validate` answers "is this file valid", not "which parser phase rejected it". | `validate` |

## Notes

- **`check` vs `validate` for the same underlying failure.** A malformed
  BenchmarkDotNet results file returns `UnsupportedSchema` (8) from `check`
  — "I can't run the requested evaluation" — but `ValidationFailed` (12)
  from `validate` — "the requested validation failed". Same exception,
  different exit code, because the two commands represent different
  operations. This is deliberate, not an inconsistency.
- **Severity-based, not presence-based.** `validate` returns `Passed` (0)
  for an artifact with only Warning-severity diagnostics; only
  Error-severity diagnostics trigger `ValidationFailed`. There is no
  `--fail-on-warning` equivalent for `validate` — validation warnings and
  regression warnings (`check`'s `Warning` status) are conceptually
  different even though both use `DiagnosticSeverity.Warning`.
- **Precedence within `check`'s `GetExitCode`:** `Regressed` > `Missing`
  (`IncompleteResultSet`) > `Unstable` (`UnstableResults`) >
  `Warning`-if-`--fail-on-warning` > `Passed`. A regression always wins
  over a missing or unstable benchmark, so CI surfaces the most
  actionable failure first.