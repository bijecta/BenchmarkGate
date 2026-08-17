# ADR-0006: Capture and Compare Benchmark Execution Environments

## Status

Accepted

## Context

`v0.5.0` ("Filesystem history") introduces history-based baselines. Instead of comparing the current benchmark results only with one explicitly supplied baseline, history mode can select several past snapshots and synthesize a baseline using an aggregation strategy such as median or trimmed mean.

This requires environment compatibility to be established before aggregation. As stated in `docs/ROADMAP.md`, aggregating measurements from incompatible execution environments may be mathematically valid while remaining operationally misleading. A history selector therefore needs structured evidence for deciding whether a stored snapshot and the current benchmark run are comparable.

BenchmarkGate currently captures no such evidence. Neither `BenchmarkObservation` nor `BenchmarkIdentity` carries execution-environment metadata. `BenchmarkIdentity` deliberately excludes environment from benchmark identity, and `BenchmarkDotNetResultParser.CompileObservation` compiles only benchmark identity, statistics, and memory measurements. The adapter does not model or compile BenchmarkDotNet's report-level host-environment data.

A real BenchmarkDotNet 0.15.8 full-JSON fixture, `CedarRecon.Tests.Performance.ExceptionClassifierBenchmark-report-full-compressed.json`, was inspected directly so that this decision is grounded in confirmed producer output rather than assumptions about BenchmarkDotNet's format. This fixture originates from CedarRecon, not BenchmarkGate; a sanitized copy belongs in BenchmarkGate's own test fixtures as part of the adapter-mapping implementation issue, so the evidence cited here remains independently reproducible from this repository. The fixture establishes the following:

- The document root contains a `HostEnvironmentInfo` object alongside `Benchmarks`. Environment metadata is therefore report-scoped, not benchmark-scoped.
- `HostEnvironmentInfo` contains 15 structured leaf fields:
  - `BenchmarkDotNetCaption`
  - `BenchmarkDotNetVersion`
  - `OsVersion`
  - `ProcessorName`
  - `PhysicalProcessorCount`
  - `PhysicalCoreCount`
  - `LogicalCoreCount`
  - `RuntimeVersion`
  - `Architecture`
  - `HasAttachedDebugger`
  - `HasRyuJit`
  - `Configuration`
  - `DotNetCliVersion`
  - `ChronometerFrequency.Hertz`
  - `HardwareTimerKind`
- Confirmed fixture values include:

  ```text
  BenchmarkDotNetVersion = "0.15.8"
  RuntimeVersion         = ".NET 10.0.10 (10.0.10, 10.0.1026.32716)"
  OsVersion              = "Windows 11 (10.0.26100.8875/24H2/2024Update/HudsonValley)"
  DotNetCliVersion       = "10.0.302"
  ```

- Benchmark entries contain no structured job or garbage-collector configuration fields. Job characteristics appear only inside `DisplayInfo` as producer-formatted presentation text, for example `Job-SNYTAA(IterationCount=10, ...)`.
- `Memory.Gen0Collections`, `Memory.Gen1Collections`, and `Memory.Gen2Collections` are measured outcomes. They do not identify the garbage-collector configuration under which the benchmark ran.

This ADR consequently covers report-level execution-environment metadata exposed through the structured `HostEnvironmentInfo` object. It does not parse `DisplayInfo`, infer configuration from measured outcomes, or establish compatibility rules for unstructured job and garbage-collector settings.

## Decision

This ADR is organized as five numbered decisions, each establishing one part of how environment metadata is modeled, captured, and used for compatibility.

### 1. Domain ownership and multi-document coherence

Execution-environment metadata is owned by a new report-level type, alongside the observations it describes:

```csharp
public sealed record BenchmarkRun(
    BenchmarkEnvironment? Environment,
    IReadOnlyList<BenchmarkObservation> Observations);
```

Environment metadata is not part of `BenchmarkIdentity` — consistent with that type's existing doc comment, which identifies *which* benchmark ran, not *where*. It is not duplicated onto every `BenchmarkObservation` either, since `HostEnvironmentInfo` is confirmed report-scoped in the fixture, not per-benchmark; attaching it per-observation would misrepresent the source data and risk observations from different runs silently carrying conflicting environment values if ever recombined.

`BenchmarkEnvironment? == null` and a present-but-partial `BenchmarkEnvironment` are distinct, deliberately unmerged states:

| State | Meaning |
|---|---|
| `null` | No environment document was supplied at all (e.g. non-full BDN JSON, or a pre-v0.5.0 baseline). |
| Present, some fields unavailable | An environment document was supplied; specific dimensions were unavailable within it. |

`BenchmarkDotNetResultParser.ParseFile` and `ParsePath` are changed to return `BenchmarkRun` directly. All callers are migrated in the same change. Because BenchmarkGate is pre-1.0 with no external parser compatibility commitment, no observation-only projection or parallel `ParseRun` entry point is retained.

When `ParsePath` compiles multiple BDN documents from a directory, those documents must represent one coherent execution environment before their observations are flattened into a single `BenchmarkRun`. A new `EnvironmentSetValidator` validates this requirement alongside the existing `ObservationSetValidator`, with their results combined through `ValidationResult.Combine`. Validation failures continue to surface through the existing `BenchmarkResultParseException` and `ValidationResult` mechanism rather than through a new exception type.

Coherence uses intersection semantics only after every input document has supplied a `HostEnvironmentInfo` object. Except for producer-defined sentinel values described in Decision 2, the aggregated `BenchmarkEnvironment` preserves a field only when every document supplies the same known value. A field known in some environment objects and unavailable in others is not a conflict and becomes `null` in the aggregate. Two different known concrete values reject the input set as incoherent.

A recognized producer-defined sentinel is preserved when every document reports that same sentinel. When some documents report a concrete value and others report the sentinel, the inputs remain coherent but the aggregated field becomes `null`, because the concrete value is not established across the complete input set.

This field-level absence must not be confused with document-level mixed presence: if some input documents contain `HostEnvironmentInfo` and others omit the entire object, `ParsePath` rejects the input set as inconsistent. Two failure modes therefore receive distinct new `BGV3xx` diagnostic codes, since they are not equally actionable for the user. The exact numeric codes (e.g. `BGV306`, `BGV307`) are not reserved by this ADR — `EnvironmentSetValidator`'s implementation issue must check the current diagnostic-code registry before assigning numbers, consistent with this project's rule against guessing a file's current shape:

| Condition | Meaning |
|---|---|
| Some documents provide `HostEnvironmentInfo`, others omit it | Inconsistent exporter configuration across the input set. |
| A known value differs between documents on any captured field | The documents represent genuinely different execution environments. |

Multi-document coherence normally uses exact typed equality independently of compatibility policy. Enum, Boolean, and numeric values use typed equality; every string field, including provenance-only fields such as `BenchmarkDotNetCaption`, uses ordinal equality on the raw producer-supplied value. The sole exception is the producer-defined sentinel treatment established in Decision 2. Future compatibility-normalization policies (the discovery item tracked in Decision 2) govern comparisons between separately compiled runs and do not weaken coherence validation within a single run.

Input coherence — whether several documents can honestly form one `BenchmarkRun` — is distinct from compatibility between separately compiled runs. Decisions 2 and 3 define the compatibility dimensions and their equality rules; Decision 4 defines their aggregate compatibility verdict. `ParsePath` resolves input coherence before any compatibility comparison occurs.

### 2. Captured fields and independent coherence/compatibility roles

BenchmarkGate captures every structured leaf field confirmed in `HostEnvironmentInfo` by the fixture — all 15 listed in Context. `DisplayInfo`'s job text is not parsed and is not captured.

Every captured field is coherence-sensitive when compiling multiple documents into one `BenchmarkRun` (Decision 1), independently of its role in compatibility comparisons between separately-compiled runs:

> Coherence sensitivity is independent of compatibility role. Every captured scalar field must be coherent across documents forming one `BenchmarkRun`, including provenance-only fields such as `BenchmarkDotNetCaption`; compatibility role governs comparisons between separate runs, not aggregation within a run.

This asymmetry exists because `BenchmarkEnvironment` is a scalar record — it cannot honestly hold two different values for one field, regardless of whether that field later participates in a compatibility verdict. A field's irrelevance to compatibility does not make conflicting values within one run acceptable.

For compatibility purposes, each captured field is classified into one of three roles. These roles govern comparisons between known values. A missing value is neither a match nor a mismatch; Decision 4 defines how unavailable dimensions contribute to the tri-state compatibility verdict.

| Role | Meaning |
|---|---|
| **Filter** | A known mismatch between two environments makes them incompatible. |
| **Advisory** | Compared and reported, but a known mismatch does not exclude a snapshot from history selection. |
| **None** | Retained as provenance only; never compared for compatibility. |

| Field | Role |
|---|---|
| `OsVersion`, `Architecture`, `RuntimeVersion`, `ProcessorName`, `PhysicalProcessorCount`, `PhysicalCoreCount`, `LogicalCoreCount`, `HasAttachedDebugger`, `HasRyuJit`, `BenchmarkDotNetVersion`, `DotNetCliVersion`, `HardwareTimerKind` | Filter |
| `Configuration`, `ChronometerFrequencyHertz` | Advisory |
| `BenchmarkDotNetCaption` | None |

Processor topology (`PhysicalProcessorCount`, `PhysicalCoreCount`, `LogicalCoreCount`) is classified Filter rather than informational: a change in visible core count can affect multithreaded behavior, thread-pool sizing, scheduling, and cache/turbo behavior, and a history selector should not treat two environments with different core counts as automatically equivalent. This has a known limitation — host core counts do not always reflect container CPU quotas or affinity restrictions — but the inability to detect every allocation difference is not a reason to ignore the differences BenchmarkDotNet does report.

`HasAttachedDebugger` and `HasRyuJit` are classified `Filter` rather than informational. A debugger attached to one run and not the other can materially affect benchmark execution. Likewise, a known difference in RyuJIT availability indicates different runtime execution capabilities, even though such differences are expected to be rare in ordinary use. BenchmarkGate captures the value without assuming that it must always be `true`.

`BenchmarkDotNetVersion` and `DotNetCliVersion` are classified Filter: both are part of the measurement toolchain rather than merely descriptive, and a history system should not silently mix results produced by different BenchmarkDotNet or SDK versions. `BenchmarkDotNetCaption` is classified None — it identifies producer/flavor but is not a precise enough signal to filter on, given `BenchmarkDotNetVersion` already provides a stronger one.

`Configuration` is classified Advisory rather than Filter, provisionally. The fixture confirms a value such as `RELEASE` exists, but does not by itself establish that this field reflects the user's benchmark assembly build configuration rather than BenchmarkDotNet's own build or host process configuration. Tracked as a non-blocking discovery item (below); classification changes only on evidence, never speculatively.

BenchmarkDotNet's `ChronometerFrequency.Hertz` is captured as `ChronometerFrequencyHertz` in `BenchmarkEnvironment`. It is classified Advisory: it is the timer's tick rate used to convert raw ticks to elapsed time, not CPU clock speed, and a difference in frequency does not by itself mean normalized nanosecond measurements are incomparable. `HardwareTimerKind` is classified Filter, since it identifies the measurement substrate itself — a change in timer mechanism is evidence the underlying measurement machinery changed, independent of its reported frequency.

The fixture's confirmed `HardwareTimerKind` value is `"Unknown"` — not a concrete mechanism such as `Tsc`, but BenchmarkDotNet's own sentinel for "timer mechanism not detected." This has a direct consequence for equality (Decision 3): a producer-defined sentinel value is not the same kind of thing as a producer-defined concrete value, and typed-enum equality must not conflate them. Three cases are distinguished:

| Case | Treatment |
|---|---|
| Recognized concrete value (e.g. `Tsc`) | A known value; compared normally under typed equality. |
| Recognized producer-defined sentinel (e.g. `Unknown`) | Semantically unavailable evidence, not a known value. Contributes `EnvironmentDimensionOutcome.Unknown`, never `Match`, even when both sides carry the sentinel. |
| Unrecognized future token | Rejected at parse time — see below. |

This has consequences for both mechanisms that operate on `HardwareTimerKind`, and the two must not be conflated: **coherence** (Decision 1) asks whether several documents can form one `BenchmarkRun`; **compatibility** (Decision 4) asks whether two separately-compiled runs match. The sentinel is treated as an ordinary known enum value for coherence — it is what the producer actually reported — but as unavailable evidence for compatibility.

Coherence, when `ParsePath` aggregates multiple documents into one `BenchmarkRun`:

| Documents report | Coherent? | Aggregated `BenchmarkEnvironment.HardwareTimerKind` |
|---|---|---|
| `Unknown` in every document | Yes — equal known values | `Unknown` (the sentinel itself is preserved) |
| A concrete value (e.g. `Tsc`) in every document | Yes — equal known values | That concrete value |
| A concrete value in some documents, `Unknown` in others | Yes — carved out as not a conflict | `null` (not proven present) |
| A concrete value in some documents, field absent in others | Yes — known-vs-missing, not a conflict | `null` |
| Two different concrete values (e.g. `Tsc` and `Hpet`) | No — incoherent, `ParsePath` rejects | n/a |

Compatibility, when comparing the aggregated environment of one run against another:

| Baseline / Current | `EnvironmentDimensionOutcome` |
|---|---|
| `Unknown` / `Unknown` | `Unknown` — a sentinel on both sides is still zero evidence, not a match |
| Concrete / `Unknown` | `Unknown` |
| Same concrete value on both sides | `Match` |
| Different concrete values | `Mismatch` |
| `null` on either side | `Unknown` |

The same distinction applies to `Architecture` or any other Filter-role enum field, should BenchmarkDotNet define an equivalent sentinel for it; this fixture confirms the case only for `HardwareTimerKind`.

Two items are tracked as non-blocking discovery issues rather than resolved here or left as untracked footnotes:

- **Discovery: Confirm BenchmarkDotNet `HostEnvironmentInfo.Configuration` semantics.** Trace how BenchmarkDotNet 0.15.8 populates the field; run a controlled Debug-versus-Release experiment; determine whether it describes the benchmark assembly, host process, or BenchmarkDotNet build; record the result using exported JSON fixtures; and propose an ADR addendum promoting it to `Filter` only if the evidence supports that change.
- **Design configurable environment compatibility normalization.** Investigate whether BenchmarkDotNet exposes structured OS, runtime, and SDK versions elsewhere; gather evidence about servicing-patch variance; consider exact, major/minor, and user-defined modes; persist the selected policy so decisions remain reproducible; and determine whether a policy change requires rebuilding history or only reselecting it.

The important asymmetry established here: `BenchmarkDotNetCaption` is compatibility-`None` but remains coherence-sensitive (Decision 1) — a field's irrelevance to cross-run compatibility does not make conflicting values within a single run acceptable.

`BenchmarkEnvironment`'s shape follows directly from the 15 captured fields and their types. Every property is nullable, reflecting that any dimension may be unavailable independent of the others (Decision 1). `Architecture` and `HardwareTimerKind` are represented as BenchmarkGate-owned Core enums, not BenchmarkDotNet's own enum types — consistent with the existing rule that BenchmarkDotNet exporter types never reach Core:

```csharp
public sealed record BenchmarkEnvironment(
    string? BenchmarkDotNetCaption,
    string? BenchmarkDotNetVersion,
    string? OsVersion,
    string? ProcessorName,
    int? PhysicalProcessorCount,
    int? PhysicalCoreCount,
    int? LogicalCoreCount,
    string? RuntimeVersion,
    BenchmarkArchitecture? Architecture,
    bool? HasAttachedDebugger,
    bool? HasRyuJit,
    string? Configuration,
    string? DotNetCliVersion,
    long? ChronometerFrequencyHertz,
    BenchmarkHardwareTimerKind? HardwareTimerKind);
```

`BenchmarkArchitecture` and `BenchmarkHardwareTimerKind` are Core-owned enums, populated by the adapter from BenchmarkDotNet's reported tokens per the recognized-concrete / recognized-sentinel / unrecognized-token distinction above. Their exact member sets are an implementation detail for the parser/compiler issue, not fixed by this ADR — only the constraint that unrecognized tokens are parse-rejected (below) and recognized sentinels map to `EnvironmentDimensionOutcome.Unknown` rather than a concrete member usable for a `Match`.

### 3. Field equality rules

Each Filter and Advisory field uses the comparison appropriate to its type:

| Field(s) | Equality rule |
|---|---|
| `Architecture`, `HardwareTimerKind` | Typed enum equality |
| `HasAttachedDebugger`, `HasRyuJit` | Boolean equality |
| `PhysicalProcessorCount`, `PhysicalCoreCount`, `LogicalCoreCount` | Numeric equality |
| `ProcessorName` | Ordinal string equality |
| `OsVersion`, `RuntimeVersion`, `DotNetCliVersion`, `BenchmarkDotNetVersion` | Ordinal string equality |
| `ChronometerFrequencyHertz` (Advisory) | Numeric equality |
| `Configuration` (Advisory) | Ordinal string equality |

BenchmarkGate performs no parsing, case folding, or normalization of string-valued filtering dimensions. `ProcessorName`, `OsVersion`, `RuntimeVersion`, `DotNetCliVersion`, and `BenchmarkDotNetVersion` are compared as raw producer-supplied ordinal strings.

This is a deliberate v0.5.0 scope decision, tested against its practical consequence: the fixture's confirmed values embed patch-level detail that can change through routine OS, runtime, and SDK servicing updates — meaning strict equality will partition a single developer's history across ordinary update cycles, potentially leaving fewer than `minimumSnapshots` compatible candidates for a period after every such update. This is accepted for v0.5.0: a strict mismatch means *not proven compatible under this policy*, not *proven performance-incompatible*. The alternative — parsing producer-formatted version strings to a coarser granularity (e.g. major.minor only) — was rejected, because BenchmarkGate does not own the semantics or formatting stability of these strings, and an arbitrary cutoff (which version component actually correlates with performance-relevant change) cannot be defended without evidence. Configurable normalization is tracked as the non-blocking discovery item defined in Decision 2.

Because strict equality can produce sparse compatible history, history-selection results must retain and expose the evidence rather than silently degrading:

- The number of candidates examined and the numbers classified `Compatible`, `Incompatible`, and `Unknown`.
- Mismatch counts and unavailable-value counts broken down by `Filter` dimension.
- The selected fallback tier from Decision 5; a latest-style fallback must never be presented as an aggregate.
- The actual and required compatible-snapshot counts when `minimumSnapshots` is unmet.
- The raw values and complete per-dimension comparisons for every examined candidate.

Default console and Markdown reporting may summarize this evidence. Structured output and verbose reporting expose the candidate-level details and raw values.

**Unrecognized enum tokens.** `Architecture` and `HardwareTimerKind` use typed enum equality. A future BenchmarkDotNet version may emit a token BenchmarkGate does not recognize. Unknown producer-supplied enum tokens must not be silently treated as absent values — `null` is reserved for genuinely unavailable evidence, and collapsing an unrecognized-but-present value into it would misrepresent a known value as missing. The adapter rejects the document with a dedicated parse diagnostic rather than deserializing it as `null` or an arbitrary enum member; absence and an unrecognized known token remain distinct states. Preserving the raw token instead was considered and rejected: it would require a richer representation than an ordinary enum, plus new equality semantics for comparing two unknown raw tokens, and that complexity is unnecessary given v0.5.0's typed-enum model.

### 4. Tri-state compatibility reduction

A Boolean compatibility result cannot distinguish "confirmed different" from "cannot be determined." BenchmarkGate uses a tri-state verdict:

```csharp
public enum EnvironmentCompatibility
{
    Compatible,
    Incompatible,
    Unknown
}
```

Only `Filter` dimensions participate in the aggregate verdict. `Advisory` dimensions are compared and reported but never influence it. `None` dimensions are retained as contextual provenance and are not compatibility comparisons.

When both environment documents are present, the reduction follows a fixed precedence, `Incompatible` > `Unknown` > `Compatible`:

| Filter-dimension results | Aggregate verdict |
|---|---|
| At least one known mismatch | `Incompatible` |
| No mismatch, but at least one dimension unavailable | `Unknown` |
| Every dimension present and equal | `Compatible` |

A known mismatch always outranks missing data: two environments with one confirmed `Architecture` mismatch and one `Unknown` `HasRyuJit` reading are `Incompatible`, not `Unknown` — missing data cannot erase positive evidence of incompatibility, though the `Unknown` dimension detail is still retained in the comparison.

When either environment document is absent, the aggregate verdict is `Unknown`. The comparison result records which document was absent, preserving the distinction between document-level absence and unavailable fields within a present environment. If both documents are present, the normal per-dimension precedence applies. Document absence short-circuits to `Unknown` because there are no two environments across which a known mismatch can be established — two environments that both lack a document are never `Compatible`; the absence of evidence is not evidence of equality.

Document absence short-circuits only the aggregate verdict, not the per-dimension detail: the comparison still emits every `Filter` and `Advisory` dimension, each with `Outcome = Unknown`. When one environment is present, its available values are retained on that side of each dimension comparison; the absent side is `null`. When both documents are absent, both raw values are `null`. This keeps `Dimensions` structurally uniform across every absence case and preserves the ability to report unavailable-dimension counts (Decision 3) even for snapshots that predate environment capture entirely.

Per-dimension detail is retained alongside the aggregate verdict, not discarded once reduced:

```csharp
public sealed record EnvironmentComparison(
    EnvironmentCompatibility Verdict,
    EnvironmentDocumentPresence BaselinePresence,
    EnvironmentDocumentPresence CurrentPresence,
    IReadOnlyList<EnvironmentDimensionComparison> Dimensions);

public enum EnvironmentDocumentPresence
{
    Present,
    Absent
}

public sealed record EnvironmentDimensionComparison(
    EnvironmentDimension Dimension,
    EnvironmentCompatibilityRole Role,
    EnvironmentDimensionOutcome Outcome,
    string? BaselineValue,
    string? CurrentValue);

public enum EnvironmentDimensionOutcome
{
    Match,
    Mismatch,
    Unknown
}
```

`EnvironmentDimensionOutcome` (per-dimension) is kept distinct from `EnvironmentCompatibility` (aggregate) — an `Advisory` dimension can register `Mismatch` as its own outcome without that mismatch affecting the aggregate verdict at all. `EnvironmentComparison.Dimensions` contains the `Filter` and `Advisory` comparisons only; `BenchmarkDotNetCaption` may appear elsewhere in the report as provenance, but it does not receive a synthetic `Match`, `Mismatch`, or `Unknown` outcome.

The full `Filter` and `Advisory` dimension-comparison list is retained for every examined history candidate. Provenance-only values remain available as contextual environment metadata but do not appear as dimension comparisons.

### 5. History eligibility, fallback ordering, and `insufficientHistory` policy

History-based baseline selection and synthesis (`median`, `trimmed-mean`, `latest`) uses only `Compatible`-verdict snapshots for the normal tier. `Unknown` snapshots are never used in aggregation and are never selected under the normal compatible-history tier; they may be selected only through the explicit `LatestUnknownFallback` mode. `Incompatible` snapshots are never selectable under any tier, including fallback.

`Unknown` is excluded from aggregation specifically because mixing it in would produce a false impression of statistical confidence: a `median` of five snapshots reads as stronger evidence than a single `latest` snapshot, even when some or all of those five came from unverified or differing machines. A single, clearly-labeled `Unknown` fallback is easier to identify, explain, and eventually replace than a contaminated aggregate.

Selection:

- If `Compatible` candidates meet or exceed `minimumSnapshots`, select the newest compatible candidates up to `window` and apply the configured history strategy. `median` and `trimmed-mean` synthesize an aggregate; `latest` selects only the newest candidate.
- The `minimumSnapshots` gate applies regardless of the configured strategy, including `latest`. With `minimumSnapshots = 3` and `strategy = latest`, `CompatibleHistory` still requires at least 3 `Compatible` candidates to exist before the (single) newest one is selected — even though only one snapshot is ever used, its use as `CompatibleHistory` rather than `LatestCompatibleFallback` depends on the minimum being satisfied. If fewer than 3 `Compatible` candidates exist, the same newest-candidate selection instead falls to `LatestCompatibleFallback`, an explicitly weaker evidence tier, even though the selected value may be identical.
- If fewer than `minimumSnapshots` compatible candidates exist, apply `insufficientHistory`:
  - With `fallback-to-latest`, select the newest `Compatible` candidate when one exists; otherwise select the newest `Unknown` candidate.
  - With `fail`, return `NoBaseline`.
- `Unknown` candidates never supplement a compatible aggregation, and `Incompatible` candidates are never selectable.
- A `Compatible` snapshot always outranks every `Unknown` snapshot regardless of recency: known compatibility outranks sample count and outranks newness. This is a normative selection rule, not merely explanatory — the selector must never prefer a newer `Unknown` candidate over an older `Compatible` one.
- If no candidate is usable under the applicable policy, return `NoBaseline`.

Precedence is conditional on policy:

```text
Normal selection:
    configured strategy over Compatible history

fallback-to-latest only:
    latest Compatible > latest Unknown > no usable baseline

fail:
    insufficient compatible history > NoBaseline
```

```csharp
public enum HistorySelectionMode
{
    CompatibleHistory,
    LatestCompatibleFallback,
    LatestUnknownFallback,
    NoBaseline
}
```

`HistorySelectionMode` names the evidence tier a baseline was built from, not the aggregation strategy used within that tier. `CompatibleHistory` means the selected candidates are `Compatible`-verdict snapshots — it applies whether the configured strategy is `median`, `trimmed-mean`, or `latest`; the latter selects only the single newest `Compatible` snapshot and performs no aggregation, yet is still `CompatibleHistory` rather than a fallback tier, since the snapshot was drawn from proven-compatible history rather than substituted for insufficient evidence. Which strategy was applied is recorded separately, as an existing policy field — `HistorySelectionMode` does not encode it.

With strategy `latest`, the selected snapshot can be identical under both `CompatibleHistory` and `LatestCompatibleFallback` — `CompatibleHistory` when the configured `minimumSnapshots` was satisfied, `LatestCompatibleFallback` when it was not and policy explicitly permitted weaker evidence. The selected value may be the same; the evidence strength it represents is not, and callers must not conflate the two modes.

`HistorySelectionMode` makes this evidence tier structural rather than a printed warning a caller could ignore; a `LatestUnknownFallback` result must carry the `EnvironmentComparison` explaining why compatibility was unproven.

`insufficientHistory` is a required, explicit two-value policy setting whenever history mode is configured — not defaulted silently, since it determines whether CI evaluates against weaker evidence, which belongs in reviewable persisted policy rather than an implicit default. It is represented internally as a typed enum rather than an unconstrained string:

```csharp
public enum InsufficientHistoryBehavior
{
    FallbackToLatest,
    Fail
}
```

| `insufficientHistory` | Fewer than `minimumSnapshots` compatible | No compatible, `Unknown` exists |
|---|---|---|
| `fallback-to-latest` | Select newest `Compatible` | Select newest `Unknown` |
| `fail` | `NoBaseline` | `NoBaseline` |

Under both values, `Incompatible` snapshots remain permanently unusable. `fail` means *do not evaluate using weaker evidence*, not that the policy configuration itself is invalid.

This precedence gives v0.5.0 a controlled migration path, since no existing snapshot carries any environment data prior to this milestone. The following table applies specifically to `insufficientHistory: fallback-to-latest`:

| Available post-upgrade history | Selection with `fallback-to-latest` |
|---|---|
| No compatible snapshots, but legacy `Unknown` snapshots exist | Newest legacy snapshot as `LatestUnknownFallback` |
| Between 1 and `minimumSnapshots - 1` compatible snapshots | `LatestCompatibleFallback` |
| At least `minimumSnapshots` compatible snapshots | `CompatibleHistory` |
| Only `Incompatible` snapshots, or no snapshots | `NoBaseline` |

With `insufficientHistory: fail`, every case with fewer than `minimumSnapshots` compatible candidates produces `NoBaseline`, even if `Compatible` or `Unknown` candidates exist.

The legacy `Unknown` fallback naturally disappears as soon as one `Compatible` snapshot exists — BenchmarkGate never prefers a newer `Unknown` snapshot over an older `Compatible` one.

**Policy validity.** History configuration is valid only when `minimumSnapshots >= 1`, `window >= 1`, and `minimumSnapshots <= window`. Violating these constraints is an invalid policy configuration, distinct from reaching `NoBaseline` with a valid policy and insufficient eligible history.

What `HistorySelectionMode.NoBaseline` maps to in terms of CLI exit code and reporting behavior is deferred to issue breakdown, not decided here. This ADR establishes only that it is an *insufficient-history* outcome, distinct from a detected regression and distinct from an invalid policy configuration.

## Consequences

**New public surface.** `BenchmarkRun`, `BenchmarkEnvironment`, `EnvironmentComparison`, `EnvironmentDimensionComparison`, and the `EnvironmentDimension` / `EnvironmentCompatibilityRole` / `EnvironmentCompatibility` / `EnvironmentDimensionOutcome` / `EnvironmentDocumentPresence` / `HistorySelectionMode` / `InsufficientHistoryBehavior` / `BenchmarkArchitecture` / `BenchmarkHardwareTimerKind` enums become new Core types. `ParseFile` and `ParsePath` change return type from `IReadOnlyList<BenchmarkObservation>` to `BenchmarkRun`, a breaking change to every existing caller, accepted per this project's pre-1.0 conventions. `EnvironmentDimensionComparison.BaselineValue`/`CurrentValue` are `string?` — each dimension's underlying typed value (bool, int, enum, etc.) rendered to its display string, not `object?`. This trades static typing for a serialization-stable, uniform representation across 15 heterogeneous field types, matching what console/JSON/Markdown reporting already needs to render regardless.

**New adapter surface.** `BdnReportRootDto` gains a `HostEnvironmentInfo` property; new `BdnHostEnvironmentDto` and `BdnChronometerFrequencyDto` types are introduced, kept internal to the adapter per the existing boundary rule that BenchmarkDotNet exporter DTOs are never exposed to Core.

**New validation surface.** `EnvironmentSetValidator` runs alongside `ObservationSetValidator`, with new `BGV3xx` diagnostic codes for mixed document-level presence and for conflicting known coherence values. Baseline document schema evolves (per ADR-0004, incrementing only the baseline document's own schema version) to persist an optional environment block; old baselines remain readable, deserializing with `Environment = null`.

**Reduced history usefulness during migration and after routine servicing.** Because environment capture has no prior history, every snapshot in existence before this milestone ships is `Unknown`. Strict, unnormalized equality on `OsVersion`, `RuntimeVersion`, `DotNetCliVersion`, and `BenchmarkDotNetVersion` means routine OS, runtime, and SDK servicing updates — and BenchmarkDotNet upgrades — can also partition history below `minimumSnapshots` after the fact. Both effects are accepted and made observable rather than silently masked: `HistorySelectionMode` and the reporting requirements in Decisions 3–5 ensure a user always knows which evidence tier a baseline came from, rather than being told "median of 5" when the real basis was one unverified snapshot.

**Two follow-on discovery items, explicitly non-blocking.** Confirming `HostEnvironmentInfo.Configuration`'s real semantics (Decision 2), and designing a future configurable/policy-controlled normalization scheme for the fields this ADR deliberately leaves unnormalized (Decision 3). Neither blocks v0.5.0 implementation.

**Deferred to issue breakdown, not this ADR.** The CLI exit code and reporting behavior for `HistorySelectionMode.NoBaseline` (Decision 5) — this ADR establishes only that it is an insufficient-history outcome, not a regression and not an invalid policy.

**Explicitly out of scope.** Structured job and GC-configuration compatibility. The fixture confirms neither is structurally available in BenchmarkDotNet's full JSON export — job characteristics exist only as unparsed presentation text in `DisplayInfo`, and `Memory`'s GC-collection counts are measured outcomes, not configuration. Should BenchmarkDotNet later expose either structurally, that is new evidence for a future ADR addendum, not a reason to parse `DisplayInfo` now.
