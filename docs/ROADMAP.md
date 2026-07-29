# Roadmap to v1.0.0

BenchmarkGate's product boundary, permanent:

**Capture** → converts native benchmark output into evidence
**Validate** → determines whether evidence/configuration is trustworthy
**Compare** → describes what changed
**Check** → decides whether the change is acceptable
**History** → selects and aggregates prior evidence
**Verify** → establishes evidence integrity
**Report** → presents results to humans and machines

Everything through v1.0.0 builds toward that boundary. Explanation
(correlating a regression with diagnostic evidence — allocations, GC,
EventPipe traces) is a real and interesting direction, but it's a
different product surface, scoped for v1.1.0+ and not part of getting to
1.0.

See `PRODUCT-SCOPE.md` for what this tool explicitly is not.

## v0.1.0-alpha.1 — shipped
## v0.2.0-alpha.1 — shipped

(see CHANGELOG.md for full detail on both)

## v0.2.x — stabilization (before starting v0.3 work)

Confirm, not implement — these should already be true; this is a checklist
to verify before building on top of them:

- [ ] baseline schema is explicitly versioned (`schemaVersion` — done, v2)
- [ ] benchmark identity is centralized (`BenchmarkIdentity` — done)
- [ ] metric units are normalized (`MetricFormatters` — done)
- [ ] parsing exceptions are separated from domain validation
      (`BenchmarkResultParseException` vs. `PolicyFileException`/
      `BaselineFileException` — done)
- [ ] commands do not contain evaluation rules (`CheckCommand` orchestrates,
      `RegressionEvaluator` decides — done)
- [ ] public JSON output has snapshot/regression tests
- [ ] `docs/BUILD-TOOL-INTEGRATION.md` exists — plain shell, NUKE, and Cake
      invocation examples. The tool already installs and runs fine under
      either build system via a standard local-tool manifest (`dotnet tool
      restore`) — this is purely a documentation gap, not a functionality
      gap, and it's cheap to close now rather than leave people guessing.
- [ ] exit-code meanings are documented in one place (currently inline on
      `ExitCodes` — consider a standalone `EXIT-CODES.md`)

## net8.0 multi-targeting (parallel track, not version-gated)

Widen the install floor from net10.0-only to net8.0+net10.0. Small,
mechanical, no design risk — do whenever convenient, doesn't block
anything below.

## v0.3.0 — Validation

New command: `benchmark-gate validate`

Validates, without running the full check pipeline:
- a policy file
- a baseline/snapshot file
- BenchmarkDotNet input
- (later) a comparison report

Core additions:
- `ValidationResult`, `ValidationDiagnostic`, `DiagnosticSeverity`,
  `DiagnosticCode`
- `PolicyValidator`, `SnapshotValidator`, `ObservationValidator`

Diagnostic codes, namespaced so they're greppable and stable:
```
BGV101  warningPercent must be lower than failurePercent
BGV203  Duplicate benchmark identity
BGV302  Mean cannot be negative
BGV401  Unsupported snapshot schema version
```

Goal: make invalid data explicit before Compare/History add more surface
that could silently misbehave on bad input.

Not yet: history aggregation, signatures, Merkle trees, profiling, plugin
loading.

## v0.4.0 — Comparison

New command: `benchmark-gate compare --baseline <path> --current <path>`

The architectural change this version exists for:

```
Before:  baseline + current + policy → evaluation
After:   baseline + current → comparison → (+ policy) → evaluation
```

`check` is refactored to consume `ComparisonResult` rather than compute
deltas itself. This is the load-bearing change of the whole roadmap —
everything from here on (history, explain, later) depends on "what
changed" being a first-class, independently-producible artifact instead
of something baked into the evaluator.

Core additions:
- `BenchmarkComparisonEngine`, `ComparisonResult`, `BenchmarkComparison`,
  `MetricComparison`
- `PercentDeltaCalculator`
- `MetricCatalog`, `MetricDescriptor`, `OptimizationDirection`

Comparison statuses: `Comparable`, `Added`, `Removed`,
`MissingReferenceMetric`, `MissingCandidateMetric`, `UnitMismatch`,
`InvalidValue`

Important: `compare` succeeds even when results are slower — it describes
change, it doesn't judge it. `check` applies policy on top of the same
`ComparisonResult`.

## v0.5.0 — Filesystem history

New commands: `benchmark-gate history append` / `history list`
New `check` flag: `--history <path>` (as an alternative to `--baseline`)

History as a plain directory, no database:
```
.benchmarkgate/history/
├── manifest.json
├── 000001.snapshot.json
├── 000002.snapshot.json
└── ...
```

Core additions:
- `BenchmarkHistoryStore` / `FileSystemHistoryStore`
- `HistoryQuery`, `HistorySelector`
- `HistoricalBaselineBuilder`, `HistoricalMetricProfile`

Initial aggregation strategies: `latest`, `median`, `trimmed-mean`.
Default: median of the last 5 compatible snapshots.

```json
{
  "baseline": {
    "mode": "history",
    "window": 5,
    "strategy": "median",
    "minimumSnapshots": 3,
    "insufficientHistory": "fallback-to-latest"
  }
}
```

**Mandatory compatibility filtering before aggregation** — suite, identity,
runtime, OS, architecture, job, GC config, environment fingerprint. A
historical baseline mixing incompatible machines is mathematically valid
and operationally misleading; this filtering is not optional.

## v0.6.0 — GitHub Action

Reusable Action that orchestrates the CLI — no core logic lives in the
Action itself.

```yaml
- uses: bijecta/benchmark-gate-action@v1
  with:
    results: BenchmarkDotNet.Artifacts/results
    policy: benchmarkgate.json
    history-artifact: benchmarkgate-history
```

Handles: downloading prior history artifacts, running validate, capturing
the current snapshot, checking against history, publishing a Markdown
summary, uploading comparison/snapshot artifacts, appending to history
only after an accepted branch run, retention guidance, GitHub annotations.

The CLI must keep working standalone, without GitHub, throughout.

### Typed build-tool integration (same theme, can land alongside v0.6.0)

Beyond the plain-shell/manual-invocation docs added in v0.2.x
stabilization, first-class typed wrappers for the two common .NET build
orchestrators:

- **NUKE** — a `Nuke.Common`-compatible generated tool wrapper (via
  `Nuke.CodeGeneration`, following the pattern of the existing
  `Nuke.Common.Tools.*` packages), giving typed method calls instead of
  raw CLI string invocation.
- **Cake** — a Cake addin package (`Cake.BenchmarkGate` or similar,
  tagged `Cake-Addin`) exposing alias methods (e.g.
  `BenchmarkGateCheck(settings => ...)`) with a typed settings object.

Not core gate functionality — this is CI/build-tool convenience, same
category as the GitHub Action. Both already work today via a plain local
tool manifest; this is strictly a DX improvement for teams already
invested in one of these build systems, not a blocker for anyone.

## v0.7.0 — Tamper-evident history

Content addressing + a hash chain (not a full Merkle tree yet).

```
contentHash = SHA-256(canonical snapshot bytes)
entryHash   = SHA-256(domain + sequence + snapshotHash + previousEntryHash)
```

New commands: `history append` (extended), `history verify`

Detects: modified snapshot, reordered entry, middle-entry deletion,
unexpected chain head, duplicated sequence, mismatched history identity.

**Limitation, stated plainly**: a hash chain is only useful when the
expected head hash is trusted. An attacker who can replace all files can
rebuild the chain. This version is tamper-evident locally, not
independently authenticated — v0.8 addresses that.

## v0.8.0 — Signed checkpoints

New commands: `history checkpoint`, `history verify --checkpoint <path>`

```json
{
  "schemaVersion": 1,
  "historyId": "cedarrecon-main-linux-x64-net10",
  "entryCount": 125,
  "headHash": "sha256:...",
  "createdAtUtc": "2026-07-28T15:30:00Z",
  "signature": { "algorithm": "ed25519", "keyId": "ci-production", "value": "..." }
}
```

Signing sources to consider, roughly in order of practicality: local dev
key, CI secret-backed key, GitHub workload identity, (much later) Sigstore
keyless signing. Sigstore/Rekor's transparency-log architecture is a good
reference model — this doesn't mean reproducing all of Sigstore.

## v0.9.0 — Reporting and governance stabilization

Freeze, before 1.0 makes it official: console output, JSON report schema,
Markdown report, GitHub summary, diagnostic codes, baseline governance,
schema migration policy, cryptographic verification format, public API
naming, compatibility rules, exit codes.

Add a decision evidence record — makes a gate result reproducible after
the fact:

```json
{
  "decisionId": "sha256:...",
  "currentSnapshotHash": "sha256:...",
  "policyHash": "sha256:...",
  "comparisonHash": "sha256:...",
  "historyCheckpoint": { "entryCount": 125, "headHash": "sha256:..." },
  "selectedHistoryEntries": [121, 122, 123, 124, 125],
  "result": "failed"
}
```

## v1.0.0 — Stable performance gate

Guaranteed stable: snapshot schema, policy schema, comparison schema, exit
codes, normalized benchmark identity, metric direction semantics, history
format, hash verification, CLI command contracts, BenchmarkDotNet support.

Supported commands: `capture`, `validate`, `compare`, `check`,
`history append`, `history list`, `history verify`, `history checkpoint`.

Explicitly **not** in v1.0 unless it's already mature by the time we get
here: experimental profiling/diagnosis.

---

## After v1.0.0 — parked, not part of this roadmap

**Mission for this phase, once it starts:** a CLI gate that can explain why
it fails, and where. Two tiers, deliberately not conflated:
- v1.4 (`explain`, cheap tier) answers *why*, at the metric level —
  allocations, GC, contention, environment drift — correlated from metrics
  already captured during a normal `check`. No extra runtime cost, no
  trace collection.
- v1.6/v1.7 (EventPipe integration) answers *where* — ties a regression to
  actual evidence (call stacks, trace data), not just a symptom category.
  Opt-in only: after a failure, in a dedicated diagnostic job, on selected
  benchmarks, or a scheduled deep-analysis run — never a default part of
  every `check`.

Don't claim "and where" in messaging until v1.6/v1.7 evidence backs it —
v1.4 alone can only honestly claim the "why" half.

Real and worth doing eventually, deliberately out of scope for the push
to 1.0 so it doesn't turn "ship a gate" into "build an observability
platform":

- Provider reader architecture (Crank, generic JSON/CSV — BenchmarkDotNet
  stays the only first-class provider through 1.0)
- Merkle checkpoints / inclusion proofs (only once basic history-plus-hash-
  chain is proven and actually needed)
- Historical variation / stability classification (MAD, CoV, trend
  detection — needs real history data to design against, which won't
  exist until v0.5+ has been used for a while)
- `explain` — correlating a regression with diagnostic evidence
- EventPipe / `dotnet-trace` / `dotnet-counters` integration
- Diagnostic comparison and explanation engine

See the architecture notes (`docs/architecture/`) for the fuller sketch of
where these could go — not committed, not scheduled.
