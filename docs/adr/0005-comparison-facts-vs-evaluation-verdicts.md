# ADR-0004: Comparison facts vs. evaluation verdicts

## Status
Accepted

## Context
v0.4.0 adds `benchmark-gate compare`, exposing benchmark comparison
results without requiring a policy. Before this milestone,
`RegressionEvaluator` did everything in one pass: match benchmarks by
identity, match metrics by name, calculate deltas, and apply policy
thresholds, all inside a single `Evaluate(observations, baseline, policy)`
call. `compare` needs the first half of that (matching and deltas)
without the second half (policy verdicts) — and `check` needs both, but
must not compute matching/deltas differently than `compare` does, or the
two commands could disagree about what changed in the same run.

This requires splitting "what changed" from "is that acceptable" into two
distinct models, owned by two distinct types, with a hard rule that
neither may duplicate the other's responsibility.

If there's one sentence that is the thesis of this ADR, it's this:
**arithmetic belongs to comparison; judgment belongs to evaluation.**
Everything below is essentially a consequence of that single rule.

Three shapes were considered for where that split lives:

1. Keep one evaluator, add a policy-free "preview mode" flag that skips
   threshold application. Rejected: the flag would need to thread through
   every method, and `compare`'s JSON output would still be shaped like a
   verdict model with unpopulated policy fields, not a genuinely
   policy-free document.
2. Two independent implementations — a new comparison engine for
   `compare`, and leave `RegressionEvaluator` computing its own deltas
   for `check`. Rejected outright: this is the exact duplication the
   milestone exists to eliminate, and the two code paths could silently
   drift (different rounding, different edge-case handling for a zero
   reference, etc.), producing different answers for the same input
   depending on which command was run.
3. One engine (`BenchmarkComparisonEngine`) that is the sole producer of
   comparison facts (`ComparisonResult`), consumed by both `compare`
   (directly, for reporting) and `RegressionEvaluator` (as its only
   input, for policy interpretation). Accepted.

## Decision
> **Architectural invariant**
>
> Comparison produces facts. Evaluation produces verdicts. Neither
> layer recreates the other's work.

- `ComparisonResult` (and its family: `BenchmarkComparison`,
  `MetricComparison`, `MetricValue`, `BenchmarkStabilityMeasurement`) is
  an immutable, policy-free document. It records facts only: which
  benchmarks are `Comparable`/`Added`/`Removed`, which metrics are
  `Comparable`/missing-on-a-side/invalid/unit-mismatched, raw values,
  absolute and percentage deltas, and a direction classification derived
  from the computed delta together with a metric's known optimization
  semantics (or `Indeterminate` when the semantics are unknown — the
  delta's sign alone is never enough, direction depends on both) —
  never a pass/fail/stability verdict. This is enforced by a
  reflection-based test asserting no `SuiteDecision` vocabulary
  (`Passed`/`Warning`/`Regressed`/`Unstable`) ever appears as a type or
  enum-member name in this model, not by review alone.
- `BenchmarkComparisonEngine.Compare(reference, candidate)` is the only
  place benchmark matching, metric matching, unit/validity checking, and
  delta calculation happen. **`BenchmarkComparisonEngine` owns
  arithmetic; `RegressionEvaluator` owns interpretation.** Arithmetic —
  absolute delta, percentage delta, zero-reference handling — is owned
  exclusively by `BenchmarkComparisonEngine`; `RegressionEvaluator`
  interprets those precomputed facts under a policy but never derives
  them again.
- `RegressionEvaluator.Evaluate(comparison, policy)` takes a
  `ComparisonResult` as its only structural input (plus the policy) and
  produces `SuiteDecision` — the policy-verdict model
  (`Passed`/`Warning`/`Regressed`/`Improved`/`Missing`/`New`/`Unstable`).
  It never re-derives what changed, only whether what changed is
  acceptable. Stability classification (the `Unstable` verdict) stays
  here, threshold-driven and policy-owned; `ComparisonResult` carries
  only the raw stability facts (`BenchmarkStabilityMeasurement`'s
  measurement count and standard deviation), never a stable/unstable
  classification, and does not persist a derived coefficient of
  variation — that's computed at evaluation time from the raw facts.
- The dependency direction is one-way: `ComparisonResult` has no
  dependency on evaluation types. `RegressionEvaluator` depends on
  `ComparisonResult`, never the reverse.
- Both `compare` and `check` call `BenchmarkComparisonEngine.Compare`
  with identical inputs to produce their `ComparisonResult`. `check`
  additionally calls `RegressionEvaluator.Evaluate` on that same result.
  There is exactly one production implementation of comparison, consumed
  two ways — not two implementations that happen to agree.
- Deterministic output ordering (`BenchmarkIdentityComparer`, comparing
  structured identity components rather than a concatenated string) is
  the engine's responsibility, not the document types'.
  `ComparisonResult`/`SuiteDecision` preserve whatever order they're
  given rather than sorting themselves.

## Consequences
- `compare`'s JSON/console/Markdown output and `check`'s policy decision
  are guaranteed to agree on "what changed" for the same run, by
  construction rather than by convention — they share one code path for
  that question.
- A new report format or a new policy rule never needs to touch
  `BenchmarkComparisonEngine`; a new kind of comparison fact (e.g. a
  metric's source unit, once available) never needs to touch
  `RegressionEvaluator`. The two concerns can evolve independently.
- Percentage-delta edge cases (a zero reference, a non-finite value) are
  handled in exactly one place (`PercentDeltaCalculator`), with an
  explicit status vocabulary rather than epsilon substitution or silent
  NaN propagation. Every consumer sees the same answer for the same
  input.
- Future consumers of benchmark facts — a dashboard, trend analysis, a
  history-aware baseline feature, an `explain` command — can depend on
  `ComparisonResult` alone, without inheriting policy semantics they
  don't need.

## Addendum

### MetricCatalog: closed and immutable, not registerable (#21)

`MetricCatalog` provides `OptimizationDirection`/canonical unit metadata
for metric names BenchmarkGate has explicit semantic knowledge of —
currently only `meanNanoseconds` and `allocatedBytesPerOperation`, the
two metrics `BenchmarkObservation` defines constants for.

A mutable/registerable catalog (letting policy files or the adapter
declare custom metric semantics at runtime) was considered and rejected
for v0.4.0: it raises open product questions — who registers a
descriptor, whether policy files can declare optimization direction and
units, what happens when policy metadata conflicts with a built-in
descriptor, whether registration is global process state, how duplicate
registrations are handled — that don't need answers yet. Catalog
membership controls semantic *classification* only, not comparison
*eligibility*: `BenchmarkComparisonEngine` compares every metric present
in the data regardless of catalog membership. An unknown metric is still
fully comparable (value, delta, percentage change); its `ChangeDirection`
simply resolves to `Indeterminate` rather than `Improvement`/`Degradation`,
since no direction can be inferred for it. Only add a built-in catalog
entry for a metric BenchmarkGate genuinely understands the semantics of
— not merely because BenchmarkDotNet or the adapter can emit it.

### UnitMismatch: reserved, not producible (#26)

`MetricComparisonStatus.UnitMismatch` remains part of the status enum —
it's part of the intended schema — but `BenchmarkComparisonEngine` never
produces it today, and its XML documentation says so explicitly.

The reason is a genuine data-model gap, not an oversight: neither
`BenchmarkObservation.Metrics` nor `BaselineEntry.Metrics` carries a
per-value source unit (`IReadOnlyDictionary<string, double>` — value
only). `MetricCatalog.Unit` is semantic metadata for a metric *name*, not
evidence of what unit a specific reported *value* used — comparing
descriptor metadata against itself can never disagree, so implementing
unit-mismatch detection against catalog metadata only would be dead code
that could never fire. `MetricValue.Unit` is therefore `string?` (null,
not empty string, when no unit is known — avoiding conflating "unknown"
with "explicitly unitless" or "malformed empty value"), populated from
`MetricCatalog` for known metrics and left null for unknown ones. Real
`UnitMismatch` detection needs baseline and candidate values to each
carry their own source-unit metadata, which is a follow-up data-model
change, not decided here.

### Rejected: persisting coefficient of variation in ComparisonResult (#26/#27)

Storing a derived coefficient of variation on `BenchmarkStabilityMeasurement`
(or elsewhere in `ComparisonResult`) was considered and rejected. A
derived value stored alongside its own inputs (measurement count,
standard deviation, and whichever mean it would be computed against)
risks the two disagreeing if one changes without the other — the same
reasoning `PercentDeltaResult` already follows by computing rather than
caching. More fundamentally, "is this coefficient of variation too high"
is a policy question, not a fact: `ComparisonResult` carries the raw
stability facts only, and `RegressionEvaluator` computes coefficient of
variation at evaluation time from those raw facts against a
policy-configured threshold. This is worth recording explicitly, since
it's the kind of addition someone will eventually propose for a reporter
or dashboard that wants to display CV without a second calculation.

### Invalid metric values: RegressionEvaluator skips them, does not recompute (#27)

A metric with `MetricComparisonStatus.InvalidReferenceValue` or
`InvalidCandidateValue` (a non-finite value on one side) has
`AbsoluteDelta`/`PercentDelta`/`Direction` all null on its
`MetricComparison`, per this ADR's core decision that only `Comparable`
metrics carry computed deltas. `RegressionEvaluator` therefore produces
no `MetricDecision` for such a metric — the same treatment as
`MissingReferenceMetric`/`MissingCandidateMetric`/`UnitMismatch` — rather
than recomputing raw arithmetic itself to reproduce the pre-v0.4.0
evaluator's behavior (which silently computed a NaN-arithmetic verdict
for non-finite values, since `RegressionEvaluator` performed its own
delta calculation before this milestone and never validated finiteness
in that loop). Recomputing would violate this ADR's boundary for the
sake of exact backward compatibility with an edge case that was never a
deliberately designed contract, only a side effect of missing
validation. This is a deliberate, documented behavior change from the
pre-v0.4.0 evaluator.

Stability classification is a separate gate and keeps reproducing legacy
non-finite-value arithmetic exactly (a NaN candidate mean produces a NaN
coefficient of variation, and `NaN > threshold` evaluates `false` in
C#, matching pre-v0.4.0 behavior) — because that arithmetic lives
entirely within `RegressionEvaluator` itself (interpreting raw
`BenchmarkStabilityMeasurement` facts under a policy threshold), not a
recomputation of something `BenchmarkComparisonEngine` already computed
and chose not to expose.

### SuiteDecision ordering follows ComparisonResult's canonical order (#27)

`SuiteDecision.Benchmarks` now reflects `ComparisonResult.Benchmarks`'
canonical `BenchmarkIdentityComparer` order, rather than the pre-v0.4.0
evaluator's caller-supplied order (observation order, then leftover
unmatched-baseline order). This is a deliberate, visible behavior change,
consistent with this ADR's ordering decision — no pre-v0.4.0 test
asserted insertion order as a contract, only repeatability of a given
input's output.
