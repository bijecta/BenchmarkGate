using System.Diagnostics;
using Xunit;

namespace Bijecta.BenchmarkGate.IntegrationTests;

/// <summary>
/// The result of running the real installed <c>benchmark-gate</c>
/// executable as a subprocess.
/// </summary>
public sealed record ToolProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Builds and packs the real <c>BenchmarkGate.Tool</c> project, installs it
/// as a real dotnet tool into an isolated <c>--tool-path</c> directory, and
/// exposes a way to run the actual installed executable/shim as a
/// subprocess -- not an in-process <c>Command.Run()</c> call. Runs once per
/// test collection (a real pack + install is too expensive to repeat per
/// test). Self-contained: works identically locally (<c>dotnet test</c>)
/// and in CI, no bespoke CI-side pack step required.
/// </summary>
public sealed class ToolInstallFixture : IAsyncLifetime
{
    private const string TestPackageVersion = "0.0.0-e2e";

    public string ExecutablePath { get; private set; } = string.Empty;

    private string _toolPathDirectory = string.Empty;
    private string _packageSourceDirectory = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var repoRoot = FindRepoRoot();
        _packageSourceDirectory = Path.Combine(Path.GetTempPath(), $"bg-e2e-source-{Guid.NewGuid()}");
        _toolPathDirectory = Path.Combine(Path.GetTempPath(), $"bg-e2e-install-{Guid.NewGuid()}");
        Directory.CreateDirectory(_packageSourceDirectory);
        Directory.CreateDirectory(_toolPathDirectory);

        var toolProjectPath = Path.Combine(repoRoot, "src", "BenchmarkGate.Tool", "BenchmarkGate.Tool.csproj");

        await RunProcessAsync("dotnet",
            $"pack \"{toolProjectPath}\" -c Release -o \"{_packageSourceDirectory}\" /p:Version={TestPackageVersion}");

        await RunProcessAsync("dotnet",
            $"tool install --tool-path \"{_toolPathDirectory}\" " +
            $"--add-source \"{_packageSourceDirectory}\" " +
            $"--version {TestPackageVersion} " +
            "Bijecta.BenchmarkGate.Tool");

        var executableName = OperatingSystem.IsWindows() ? "benchmark-gate.exe" : "benchmark-gate";
        ExecutablePath = Path.Combine(_toolPathDirectory, executableName);

        if (!File.Exists(ExecutablePath))
        {
            throw new InvalidOperationException(
                $"Expected installed tool executable at '{ExecutablePath}' but it does not exist. " +
                "Pack/install may have failed silently -- check the process output captured above.");
        }
    }

    public ValueTask DisposeAsync()
    {
        TryDeleteDirectory(_toolPathDirectory);
        TryDeleteDirectory(_packageSourceDirectory);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Runs the real installed executable with the given raw argument
    /// string, capturing exit code and both output streams.
    /// </summary>
    public async Task<ToolProcessResult> RunAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{ExecutablePath}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ToolProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup -- e.g. a locked file from antivirus
            // scanning the freshly-installed executable on Windows
            // shouldn't fail the whole test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Same reasoning as above.
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BenchmarkGate.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate BenchmarkGate.sln walking up from the test output directory " +
                $"('{AppContext.BaseDirectory}'). This fixture assumes it's running from within the repo.");
    }

    private static async Task RunProcessAsync(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName} {arguments}' exited with code {process.ExitCode}.\n" +
                $"stdout:\n{stdout}\nstderr:\n{stderr}");
        }
    }
}

[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Collection\" is xUnit's own naming idiom for a CollectionDefinition marker class, not a BCL collection type.")]
public sealed class ToolInstallCollection : ICollectionFixture<ToolInstallFixture>
{
    public const string Name = "Tool Install";
}