using System.IO.Compression;
using System.Text.Json;

namespace Pacmine.Core;

/// <summary>
/// Provides static methods to parse meta info from package archive.
/// </summary>
public static class PackageParser
{
    /// <summary>
    /// The name of the meta file in the package archive.
    /// </summary>
    public const string META_FILE_NAME = ".PACMINE.META.json";

    /// <summary>
    /// Reads the meta info from the package archive.
    /// </summary>
    /// <param name="archive">The package archive.</param>
    /// <returns>The meta info, or <c>null</c> if meta file is missing or corrupted.</returns>
    public static PackageMeta? GetMeta(ZipArchive archive)
    {
        var metaEntry = archive.GetEntry(META_FILE_NAME);
        if (metaEntry == null)
            return null;
        using var stream = metaEntry.Open();
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<PackageMeta>(reader.ReadToEnd());
    }

    /// <summary>
    /// Gets the time when the package was packaged(actually the last modified time of
    /// the meta file is used as a proxy for "packaged time").
    /// </summary>
    /// <param name="archive">The package archive.</param>
    /// <returns>The packaged time, or <c>null</c> if meta file is missing.</returns>
    public static DateTime? GetPackagedTime(ZipArchive archive)
    {
        // take the modified time of meta file as the packaged time
        var metaEntry = archive.GetEntry(META_FILE_NAME);
        return metaEntry?.LastWriteTime.LocalDateTime;
    }
}