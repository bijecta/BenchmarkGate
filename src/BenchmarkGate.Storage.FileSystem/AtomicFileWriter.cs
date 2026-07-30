using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Storage.FileSystem;

/// <summary>
/// Writes files atomically (write to a temp file, then move into place),
/// creating the target directory first if it doesn't exist. Shared by every
/// reporter/baseline writer so this logic — and any bug fixes to it — lives
/// in exactly one place. See master spec section 19 (atomic output-file
/// writes).
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicity vs. persistence</b> — these are different guarantees.
/// Atomicity means a reader sees either the old file or the new file, never
/// a partially written destination. This type provides atomicity via a
/// same-directory temp file + <see cref="File.Move(string,string,bool)"/>.
/// When <c>flushToDisk</c> is enabled, the temporary file's buffered data is
/// explicitly flushed before the rename — this improves resistance to data
/// loss but does NOT constitute a universal crash-durability guarantee for
/// the final renamed file: strict durability of the rename itself may also
/// require flushing the containing directory afterward, which is
/// filesystem- and OS-specific and not exposed by a straightforward
/// cross-platform managed API in .NET. <c>flushToDisk</c> defaults to off —
/// for CI benchmark reports, a sudden power loss immediately after a report
/// write is normally irrelevant, and forcing disk flushes adds latency for
/// no benefit in that case.
/// </para>
/// <para>
/// <b>Qualified atomicity claim</b>: this provides a same-filesystem rename
/// for atomic replacement on supported local filesystems. .NET's own docs
/// for <c>File.Move(..., overwrite: true)</c> do not promise universal
/// atomicity across every filesystem, network share, container mount, or
/// cloud-mounted directory — this matters if reports are ever written to
/// NFS/SMB/Docker bind mounts.
/// </para>
/// <para>
/// <b>Concurrent writers to the same path</b>: the GUID in the temp filename
/// prevents two writers from corrupting each other's temp file, but does NOT
/// coordinate writers targeting the same final path — with two concurrent
/// writers, both serialize successfully to their own temp file and both call
/// <c>File.Move</c>; whichever commits last wins. This is acceptable for
/// BenchmarkGate's current usage (one <c>check</c>/<c>capture</c>
/// invocation writing its own output), but is documented here as
/// last-writer-wins, not as coordinated concurrent writing. If that ever
/// needs to be an error instead, use a separate lock file created with
/// <see cref="FileMode.CreateNew"/> — not an in-process lock, since that
/// wouldn't coordinate separate CLI processes.
/// </para>
/// </remarks>
public static class AtomicFileWriter
{
    private const int DefaultBufferSize = 64 * 1024;

    /// <summary>
    /// UTF-8 without a byte-order mark, with strict fallback (throws rather
    /// than silently replacing malformed surrogate sequences). Explicit
    /// rather than relying on StreamWriter's own UTF-8-no-BOM default so the
    /// file contract is unambiguous at the call site.
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Writes plain text content atomically. When <paramref name="overwrite"/>
    /// is false, the underlying commit (File.Move with overwrite: false) is
    /// the actual enforcement point — not a preceding File.Exists check,
    /// which would leave a time-of-check/time-of-use race between the check
    /// and the write.
    /// </summary>
    public static void Write(string path, string content, bool overwrite = true, bool flushToDisk = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var paths = PrepareTempPath(path);

        try
        {
            using (var stream = CreateTempStream(paths.TempPath))
            using (var writer = new StreamWriter(
                       stream,
                       Utf8NoBom,
                       bufferSize: DefaultBufferSize,
                       leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();

                if (flushToDisk)
                {
                    stream.Flush(flushToDisk: true);
                }
            }

            Commit(paths.TempPath, paths.FullPath, overwrite);
        }
        finally
        {
            TryDeleteTempFile(paths.TempPath);
        }
    }

    /// <summary>
    /// Serializes <paramref name="value"/> directly into the destination file
    /// atomically, without ever holding the full serialized JSON as one
    /// in-memory string. See <see cref="Write"/> for the overwrite semantics.
    /// </summary>
    public static void WriteJson<T>(
        string path,
        T value,
        JsonSerializerOptions? options = null,
        bool overwrite = true,
        bool flushToDisk = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var paths = PrepareTempPath(path);

        try
        {
            using (var stream = CreateTempStream(paths.TempPath))
            {
                JsonSerializer.Serialize(stream, value, options ?? DefaultJsonOptions);

                if (flushToDisk)
                {
                    stream.Flush(flushToDisk: true);
                }
            }

            Commit(paths.TempPath, paths.FullPath, overwrite);
        }
        finally
        {
            TryDeleteTempFile(paths.TempPath);
        }
    }

    private static FileStream CreateTempStream(string tempPath)
    {
        // CreateNew (not Create): formally guarantees this call never
        // overwrites another temp file, rather than relying on the GUID
        // making a collision merely "extremely unlikely".
        //
        // No FileOptions.SequentialScan: that hint is documented for
        // sequential READS of large files; this stream is write-only, so it
        // communicates no useful intent here.
        return new FileStream(
            tempPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = DefaultBufferSize,
            });
    }

    private static void Commit(string tempPath, string destinationPath, bool overwrite)
    {
        // File.Move(..., overwrite: false) is itself the atomic check —
        // if the destination exists, this throws IOException without a
        // preceding File.Exists probe, so there is no window between
        // checking and writing for another process to race into.
        File.Move(tempPath, destinationPath, overwrite);
    }

    private static AtomicPaths PrepareTempPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileName = Path.GetFileName(fullPath);
        var tempFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";

        var tempPath = Path.Combine(directory ?? Directory.GetCurrentDirectory(), tempFileName);

        return new AtomicPaths(fullPath, tempPath);
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        // Cleans up the temp file if serialization/write/commit failed
        // partway through. Deliberately swallows failures here — the
        // original write/commit exception (if any) is what the caller
        // should see; a stale temp file can be cleaned up separately and
        // isn't itself worth masking the real error. Note that after a
        // successful commit the temp path no longer exists (it was moved),
        // so File.Delete is a harmless no-op in the common case — this
        // suppression only ever matters when there was already another
        // failure in progress.
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct AtomicPaths(string FullPath, string TempPath);
}