# Product Scope

## What BenchmarkGate is

A local-first performance contract gate for BenchmarkDotNet output.
Detects, evaluates, tracks, and (post-1.0) explains .NET performance
regressions in CI — a CLI gate that can explain why it fails, and where.

## What BenchmarkGate is not

Read this before adding anything that isn't already on the roadmap.

- **Not a benchmark runner.** BenchmarkDotNet remains the measurement
  engine. BenchmarkGate reads its output; it never runs benchmarks itself.
- **Not an APM.** No always-on monitoring, no live dashboards, no
  request-tracing across a running service.
- **Not a general profiler.** Diagnostic evidence collection (post-1.0) is
  opt-in, scoped to a specific failed benchmark or a scheduled deep-analysis
  run — never continuous, never a PerfView/dotnet-trace replacement.
- **Not a blockchain.** History integrity (v0.7–v0.8) uses a hash chain
  and signed checkpoints because those are the minimum needed to detect
  tampering — not because more cryptography is inherently better. Merkle
  trees are deferred until inclusion/consistency proofs solve a real,
  demonstrated need.
- **Not a hosted SaaS.** No account, no hosted database, no dashboard only
  CI can reach. Baselines, policies, and history are repo-local files. The
  same command produces the same decision locally and in CI.
- **Does not prove benchmark execution was honest.** Integrity verification
  proves a snapshot/history entry wasn't tampered with after capture. It
  says nothing about whether the underlying benchmark run itself was fair,
  representative, or free of measurement error — that's BenchmarkDotNet's
  and the user's responsibility.
- **Does not guarantee identical hardware.** Environment compatibility
  checking (v0.5+) filters out obviously incompatible comparisons; it
  doesn't guarantee that two "compatible" environments produce
  numerically comparable results down to the last percent.

## The Explain boundary

Explain (post-1.0) correlates a regression with diagnostic evidence and
suggests an investigation direction. It never:

- Claims causation. Language is always "correlates with," "likely
  explanation," "possible contributor," "investigation direction" —
  never "caused by."
- Changes a gate decision. Check decides pass/fail from Compare + Policy
  alone. Explain runs after, adds understanding, never feeds back into
  the exit code.
- Runs by default. Diagnostic collection (EventPipe, `dotnet-trace`) only
  happens opt-in — after a failure, on selected benchmarks, or on a
  scheduled job. A normal `check` never silently gets slower because
  Explain exists.

## Using this document

If a feature request or design idea doesn't fit under "What BenchmarkGate
is," or it directly contradicts an item in "What BenchmarkGate is not,"
that's a signal to write a `ROADMAP.md` "parked" entry or reject it
outright — not to quietly build it into Core anyway. This document should
make saying no easy, not require re-litigating scope every time.
