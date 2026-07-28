using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Bijecta.BenchmarkGate.Tool.Tests;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "benchmarkgate-tests-" + Guid.NewGuid().ToString("N"));

    public AtomicFileWriterTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string PathIn(params string[] segments) =>
        Path.Combine([_tempDirectory, .. segments]);

    [Fact]
    public void Write_CreatesMissingDirectory()
    {
        var target = PathIn("nested", "does", "not", "exist", "report.md");

        AtomicFileWriter.Write(target, "hello");

        File.Exists(target).Should().BeTrue();
        File.ReadAllText(target).Should().Be("hello");
    }

    [Fact]
    public void Write_ReplacesExistingFile()
    {
        var target = PathIn("report.md");
        File.WriteAllText(target, "old content");

        AtomicFileWriter.Write(target, "new content");

        File.ReadAllText(target).Should().Be("new content");
    }

    [Fact]
    public void Write_UsesUtf8WithoutBom()
    {
        var target = PathIn("report.md");

        AtomicFileWriter.Write(target, "hello");

        var bytes = File.ReadAllBytes(target);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        hasBom.Should().BeFalse();
    }

    [Fact]
    public void Write_DoesNotLeaveTempFile_AfterSuccessfulCommit()
    {
        var target = PathIn("report.md");

        AtomicFileWriter.Write(target, "hello");

        Directory.GetFiles(_tempDirectory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void WriteJson_ProducesValidJson()
    {
        var target = PathIn("decision.json");

        AtomicFileWriter.WriteJson(target, new { Name = "value", Count = 3 });

        var act = () => JsonDocument.Parse(File.ReadAllText(target));
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteJson_OmitsNullPropertiesByDefault()
    {
        var target = PathIn("decision.json");

        AtomicFileWriter.WriteJson(target, new NullableSample(Value: null));

        File.ReadAllText(target).Should().NotContain("Value");
    }

    [Fact]
    public void WriteJson_RemovesTempFile_WhenSerializationFails()
    {
        var target = PathIn("decision.json");
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ThrowingConverter<ThrowingSample>());

        var act = () => AtomicFileWriter.WriteJson(target, new ThrowingSample(), options);

        act.Should().Throw<InvalidOperationException>();
        File.Exists(target).Should().BeFalse();
        Directory.GetFiles(_tempDirectory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void WriteJson_DoesNotModifyExistingDestination_WhenSerializationFails()
    {
        var target = PathIn("decision.json");
        File.WriteAllText(target, "original content");
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ThrowingConverter<ThrowingSample>());

        var act = () => AtomicFileWriter.WriteJson(target, new ThrowingSample(), options);

        act.Should().Throw<InvalidOperationException>();
        File.ReadAllText(target).Should().Be("original content");
    }

    [Fact]
    public void Write_WithOverwriteFalse_ThrowsWhenDestinationAlreadyExists()
    {
        var target = PathIn("report.md");
        File.WriteAllText(target, "original content");

        var act = () => AtomicFileWriter.Write(target, "new content", overwrite: false);

        act.Should().Throw<IOException>();
        File.ReadAllText(target).Should().Be("original content");
    }

    [Fact]
    public void Write_WithOverwriteFalse_SucceedsWhenDestinationDoesNotExist()
    {
        var target = PathIn("report.md");

        AtomicFileWriter.Write(target, "content", overwrite: false);

        File.ReadAllText(target).Should().Be("content");
    }

    [Fact]
    public void WriteJson_WithOverwriteFalse_ThrowsWhenDestinationAlreadyExists()
    {
        var target = PathIn("decision.json");
        File.WriteAllText(target, "original content");

        var act = () => AtomicFileWriter.WriteJson(target, new { Value = 1 }, overwrite: false);

        act.Should().Throw<IOException>();
        File.ReadAllText(target).Should().Be("original content");
    }

    private sealed record NullableSample(string? Value);

    private sealed record ThrowingSample;

    private sealed class ThrowingConverter<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("partial", "content");
            throw new InvalidOperationException("Expected serialization failure.");
        }
    }
}