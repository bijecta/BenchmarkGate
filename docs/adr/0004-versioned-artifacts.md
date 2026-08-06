# ADR-0004: Versioned artifacts

## Status
Accepted

## Context
BenchmarkGate persists multiple document types: baseline snapshots,
policies, comparison reports, decision reports, and (in the future)
history manifests and checkpoints. Each document evolves independently:
v0.3.0 added validation diagnostics without touching the comparison
model; v0.4.0 added a comparison document without touching the decision
document's schema. Without an explicit version marker, an unsupported
schema version risks two failure modes: a newer BenchmarkGate silently
misreading an older file (or vice versa), or an unrelated document type's
version being conflated with another's — exactly what
`JsonComparisonReporter` (#28) deliberately avoided by giving its
comparison document its own `schemaVersion = 1`, independent of
`JsonDecisionReporter`'s counter, rather than sharing one.

Two shapes were considered for expressing a document's schema version:

1. Implicit schema detection — attempt to deserialize against the newest
   schema, fall back to older shapes on failure. Rejected: structural
   inference is inherently heuristic. Two schema versions may deserialize
   successfully while representing different semantics, producing
   incorrect results instead of a deterministic failure.
2. An explicit `"schemaVersion"` integer field on every persisted
   document, checked before anything else is trusted. An unsupported
   version is a load-time error with a clear message. Accepted.

## Decision
- Every persisted artifact carries `"schemaVersion"`: snapshot (baseline),
  policy, comparison, decision, and (when built) history manifest and
  checkpoint.
- Each persisted document owns its own version sequence. A comparison
  report moving from `schemaVersion` 1 to 2 does not imply any change to
  the decision report schema, baseline schema, or policy schema.
- Document readers validate `schemaVersion` before attempting to interpret
  the remainder of the document. Unsupported versions fail deterministically
  with a typed error — not a silent best-effort read, not a warning that's
  easy to miss, not an attempt to coerce old data into a new shape.
- Breaking schema changes are acceptable pre-1.0, with no migration path.
  BenchmarkGate does not yet expose a supported persistence compatibility
  surface (same precedent as Core API breaks — e.g. `RegressionEvaluator`'s
  v0.4.0 signature change).
- Post-1.0, schema stability becomes part of the v1.0.0 guarantee — a
  breaking schema change after that point needs a migration path, not just
  a version bump. (See `BASELINE-GOVERNANCE.md` once that exists.)

## Consequences
- A person who runs an old `benchmark-gate` binary against a newer
  baseline/policy/report file gets a clear "unsupported schema version"
  error, not a wrong or partial result.
- Each document type can evolve its schema on its own timeline — adding
  the comparison document in v0.4.0 required no changes to the decision
  document's schema or version counter, and vice versa.
- Every new persisted document type this project adds needs its own
  `schemaVersion` from the start — this isn't optional per-type, it's the
  standing convention.
- Before 1.0, evolving a schema typically consists of incrementing the
  document's `schemaVersion` constant and updating its reader/writer,
  without maintaining backward compatibility. After 1.0, equivalent
  changes require an explicit compatibility or migration strategy.
- Schema versioning applies only to persisted artifacts. It is
  intentionally independent of BenchmarkGate's assembly version, NuGet
  package version, or CLI version.
