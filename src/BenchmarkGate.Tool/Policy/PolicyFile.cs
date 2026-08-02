using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Policy;
using Bijecta.BenchmarkGate.Core.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Tool.Policy;

public sealed class PolicyFileException : Exception
{
    public string SourceFile { get; }
    public ValidationResult? ValidationResult { get; }

    public PolicyFileException(string sourceFile, string message)
        : base($"{message} (source file: '{sourceFile}')")
    {
        SourceFile = sourceFile;
    }

    public PolicyFileException(string sourceFile, string message, Exception innerException)
        : base($"{message} (source file: '{sourceFile}')", innerException)
    {
        SourceFile = sourceFile;
    }

    private PolicyFileException(string sourceFile, string message, ValidationResult validationResult)
        : base(message)
    {
        SourceFile = sourceFile;
        ValidationResult = validationResult;
    }

    internal static PolicyFileException FromValidationResult(string sourceFile, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsValid)
        {
            throw new ArgumentException(
                "A valid result cannot be converted into a policy validation exception.", nameof(result));
        }

        return new PolicyFileException(sourceFile, BuildMessage(sourceFile, result), result);
    }

    private static string BuildMessage(string sourceFile, ValidationResult result)
    {
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var lines = errors.Select(d => $"  {d.Descriptor.Id} {d.Path}: {d.Message}");

        return $"Policy '{sourceFile}' contains {errors.Count} validation error(s):" +
               Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Reads the policy.json file format. Load compiles into a usable
/// GatePolicy (throwing on invalid input); Validate returns the raw
/// ValidationResult without compiling — used by `benchmark-gate validate`
/// to surface Warning-severity findings that Load discards on success.
/// Both share the same Deserialize step. See ADR-0003.
/// </summary>
public static class PolicyFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static GatePolicy Load(string path)
    {
        var document = Deserialize(path);

        var validation = PolicyValidator.Validate(document);
        if (!validation.IsValid)
            throw PolicyFileException.FromValidationResult(path, validation);

        return PolicyCompiler.CompileValidated(document);
    }

    public static ValidationResult Validate(string path)
    {
        var document = Deserialize(path);
        return PolicyValidator.Validate(document);
    }

    private static PolicyDocument Deserialize(string path)
    {
        if (!File.Exists(path))
            throw new PolicyFileException(path, "Policy file does not exist.");

        PolicyDocument? document;
        try
        {
            using var stream = File.OpenRead(path);
            document = JsonSerializer.Deserialize<PolicyDocument>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new PolicyFileException(path, "Policy file has invalid JSON syntax or structure.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PolicyFileException(path, "Access to the policy file was denied.", ex);
        }
        catch (IOException ex)
        {
            throw new PolicyFileException(path, "Could not read policy file.", ex);
        }

        if (document is null)
            throw new PolicyFileException(path, "Policy file deserialized to null.");

        return document;
    }
}