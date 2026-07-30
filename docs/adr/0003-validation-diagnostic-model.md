# ADR-0003: Validation diagnostic model

## Status
Accepted

## Context
v0.3.0 adds `benchmark-gate validate`, which needs to collect every
problem in a document in one pass (unlike PolicyFile.Load/BaselineFile.Load
today, which fail fast). This requires a shared representation for "one
validation finding" used by PolicyValidator, SnapshotValidator, and
ObservationValidator alike.

Three shapes were considered for identifying *which kind* of finding a
diagnostic is:

1. A flat `enum DiagnosticCode` in Core, with every code as a member.
   Rejected: PolicyValidator/SnapshotValidator are Core, but
   ObservationValidator is the BenchmarkDotNet adapter project. An enum
   owned by Core can't grow new members from a project Core doesn't
   depend on without inverting ADR-0001's dependency direction.
2. A `const string` per code (mirroring `BenchmarkObservation.MeanNanosecondsMetric`).
   Simple, but every code is then just a bare string at every call site —
   no title, no default severity, no help link, and no way to later
   generate documentation or support a `benchmark-gate explain BGV101`
   command without a second lookup table.
3. A descriptor type (`DiagnosticDescriptor`), Roslyn-style: an immutable
   record carrying the code's stable metadata, defined once per code by
   the validator that owns it, referenced everywhere that code is
   reported. Accepted.

## Decision
- `DiagnosticDescriptor` (Id, Title, DefaultSeverity, HelpLink) is the
  stable identity of one kind of finding. `HelpLink` is nullable and
  unset until a docs site exists — no URL scheme is committed to yet.
- Each validator owns its own code range (BGV1xx/2xx/3xx per
  docs/ROADMAP.md) via an `internal static class <Validator>Diagnostics`
  holder colocated with that validator, not a shared central registry.
  The descriptors are an implementation detail of the validator; callers
  consume them only through `ValidationDiagnostic.Descriptor`, not by
  referencing the holder class directly. This keeps ownership clear and
  needs no cross-project dependency.
- `DiagnosticSeverity` has exactly two members: `Warning`, `Error`. A
  third `Info` tier, for processing notes such as "using default
  threshold", was considered and rejected for the current model —
  every diagnostic is meant to be something the user may need to fix
  or review, not a status message. If genuine informational-note use
  cases emerge later, they should preferably be represented by a
  distinct concept, such as `ValidationResult.Messages`, rather than
  diluting `DiagnosticSeverity`'s meaning. Adding an `Info` member
  remains an additive alternative, but would require reviewing
  consumers for exhaustive switches, filtering, serialization, and
  output behavior.
- `ValidationDiagnostic.Severity` always equals `Descriptor.DefaultSeverity`
  — no per-instance override exists. If one is ever needed, add
  `DiagnosticSeverity? OverrideSeverity` with a computed
  `EffectiveSeverity`, rather than storing `Severity` independently now.
- `ValidationDiagnostic` carries no source-file identity. A
  `ValidationResult` represents the validation of one logical input,
  whose identity (which file/document) is retained by the caller
  (the CLI command), not duplicated onto every diagnostic. Add
  source-level location only when a single validation operation can
  produce findings spanning multiple independently addressable inputs
  — e.g. if ObservationValidator ever treats a directory of
  BenchmarkDotNet output as one logical input but needs to point
  diagnostics at individual files within it (a question for Issue 4,
  not decided here). If that need arises, prefer `Source`/`SourcePath`
  over `SourceFile`, since inputs may be directories, stdin, or
  generated snapshots, not just files.
- `ValidationDiagnostic.Path` is a bare, unstructured `string`. Each
  validator's addressing convention differs (JSON pointers for
  PolicyValidator/SnapshotValidator; still open for ObservationValidator,
  decided in Issue 4) and a shared structured location type is not
  introduced until concrete needs from more than one validator justify it.

## Consequences
- Adding a new diagnostic code does not require changing Core's public
  validation model. It only adds a descriptor instance to the owning
  validator's internal holder. For adapter-owned diagnostics, it does
  not touch Core at all.
- Diagnostic IDs become part of BenchmarkGate's machine-readable
  compatibility contract once released. Titles, messages, and help
  links may evolve, but an existing ID must not be reassigned to a
  different meaning.
- `PolicyValidatorDiagnostics.All` / `SnapshotValidatorDiagnostics.All`
  (added when those holders are written) enable uniqueness tests and
  are the future basis for a documentation-generation step or an
  `explain` command, without redesigning this model.
- If cross-input diagnostics or informational-tier findings are needed
  later, both are additive changes to this model, not replacements
  of it.

## Addendum

### PolicyValidator implementation (#4)

Two additional decisions emerged while implementing the first consumer
of this model:

- **Dictionary values are nullable.** `PolicyDocument.Metrics` is typed
  as `IReadOnlyDictionary<string, MetricDefinition?>`, not
  `IReadOnlyDictionary<string, MetricDefinition>`. A policy document
  such as `"metrics": { "meanNanoseconds": null }` deserializes
  successfully: it is syntactically valid JSON, but semantically
  invalid policy input. The validator therefore reports a dedicated
  `MissingMetricDefinition` diagnostic instead of failing with a
  `NullReferenceException` on first dereference.

  Every nullable field represented by a Core document model must be
  treated as reachable from syntactically valid JSON and checked
  explicitly. Validators must not assume a value is present merely
  because ordinary generated policy files would normally contain it.

- **Missing and unsupported schema versions are distinct diagnostics.**
  `MissingSchemaVersion` and `UnsupportedSchemaVersion` use separate
  diagnostic IDs. An absent schema version and a schema version the
  current build does not recognize require different remediation:
  adding the property versus migrating or using a compatible document.
  Keeping them distinct also allows future migration tooling and
  report consumers to handle the two cases independently. Once
  released, these IDs remain separate machine-readable compatibility
  contracts and must not later be merged or reassigned.

Both decisions are consistent with the original validation model. They
add collectible findings that the initial design did not enumerate;
they do not change `DiagnosticSeverity`, `DiagnosticDescriptor`,
`ValidationDiagnostic`, or `ValidationResult`.