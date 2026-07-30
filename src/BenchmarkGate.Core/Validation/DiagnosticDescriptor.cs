namespace Bijecta.BenchmarkGate.Core.Validation;

/// <summary>
/// The stable, documented identity of one kind of validation finding.
/// Defined once per BGVxxx code by the validator that owns that code range
/// (see docs/ROADMAP.md's BGV1xx/BGV2xx/BGV3xx/BGV4xx convention — e.g.
/// PolicyValidator's internal PolicyValidatorDiagnostics holder class),
/// reused across every ValidationDiagnostic reported under it. See
/// ADR-0003 for why this is a descriptor type rather than an enum or a
/// bare const string.
/// </summary>
/// <param name="Id">
/// The stable code, e.g. "BGV101". Never change an existing descriptor's
/// Id after a stable release — see ExitCodes' stability rule; the same
/// reasoning applies here.
/// </param>
/// <param name="Title">
/// Short, human-readable summary of what this diagnostic means, e.g.
/// "Warning threshold must be less than failure threshold". Used as the
/// CLI/report heading; <see cref="ValidationDiagnostic.Message"/> carries
/// the instance-specific detail (which field, which values).
/// </param>
/// <param name="DefaultSeverity">The severity every instance of this code is reported at.</param>
/// <param name="HelpLink">
/// Optional URL to further documentation for this code. Null until a docs
/// site exists to point it at — don't invent a URL scheme ahead of that.
/// </param>
public sealed record DiagnosticDescriptor(
    string Id,
    string Title,
    DiagnosticSeverity DefaultSeverity,
    string? HelpLink = null);