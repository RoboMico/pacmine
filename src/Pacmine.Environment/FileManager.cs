using System.Security.Cryptography;

namespace Pacmine.Environment;

/// <summary>
/// Provides pure filesystem operations for Pacmine environments:
/// copying files with SHA-256 checksum computation, removing owned files,
/// pruning stale files, and checking for file conflicts.
/// All methods are static and stateless — callers pass paths and pre-computed ownership data.
/// </summary>
public static class FileManager
{
    /// <summary>
    /// Copies files from a source directory into the environment root, computing SHA-256 checksums.
    /// Stale files (previously owned but no longer in the source) are pruned.
    /// </summary>
    /// <param name="rootPath">The environment root path.</param>
    /// <param name="source">The source directory containing files to install.</param>
    /// <param name="previouslyOwnedFiles">
    /// Set of relative paths previously owned by this package.
    /// Files in this set that are not present in <paramref name="source"/> will be deleted.
    /// Pass an empty set for new installs.
    /// </param>
    /// <returns>A dictionary mapping each relative file path to its SHA-256 checksum.</returns>
    public static Dictionary<string, string> UpdateFiles(
        string rootPath,
        DirectoryInfo source,
        HashSet<string>? previouslyOwnedFiles)
    {
        if (!source.Exists)
            throw new DirectoryNotFoundException($"Source directory '{source.FullName}' does not exist");

        var fileList = new Dictionary<string, string>();
        var sourceFiles = source.EnumerateFiles("*", SearchOption.AllDirectories);

        var staleFiles = previouslyOwnedFiles != null
            ? new HashSet<string>(previouslyOwnedFiles)
            : [];

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(source.FullName, sourceFile.FullName);
            var targetPath = Path.Combine(rootPath, relativePath);

            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null)
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceFile.FullName, targetPath, overwrite: true);

            // Compute SHA256 checksum
            using var sha256 = SHA256.Create();
            using var fs = sourceFile.OpenRead();
            var hashBytes = sha256.ComputeHash(fs);
            var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

            fileList[relativePath] = checksum;
            staleFiles.Remove(relativePath);
        }

        // Remove stale files that are no longer in the source directory
        foreach (var staleFile in staleFiles)
        {
            var fullPath = Path.Combine(rootPath, staleFile);
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch
            {
                // best-effort deletion
            }
        }

        return fileList;
    }

    /// <summary>
    /// Deletes the specified files from the environment root.
    /// Best-effort: exceptions during deletion are swallowed.
    /// </summary>
    /// <param name="rootPath">The environment root path.</param>
    /// <param name="filePaths">Relative paths of the files to remove.</param>
    public static void RemoveFiles(string rootPath, IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            var fullPath = Path.Combine(rootPath, filePath);
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch
            {
                // best-effort deletion
            }
        }
    }

    /// <summary>
    /// Checks whether the specified file names conflict with managed or orphan files.
    /// </summary>
    /// <param name="rootPath">The environment root path.</param>
    /// <param name="fileNames">The file names (relative paths) to check for conflicts.</param>
    /// <param name="managedFiles">
    /// A mapping of relative file paths to their owning package names.
    /// Pre-computed by the caller from the registry.
    /// </param>
    /// <param name="ignoredOwners">
    /// Package names whose files should be ignored when checking for conflicts.
    /// Pass an empty array to check against all managed files.
    /// </param>
    /// <returns>
    /// A dictionary mapping each conflicting file name to its owner.
    /// An empty owner string indicates an orphan file (exists on disk but not managed by any package).
    /// </returns>
    public static Dictionary<string, string> CheckConflictFiles(
        string rootPath,
        string[] fileNames,
        Dictionary<string, string> managedFiles,
        string[] ignoredOwners)
    {
        var conflicts = new Dictionary<string, string>();

        foreach (var fileName in fileNames)
        {
            // Check if the file is managed by a package not in the ignored list
            if (managedFiles.TryGetValue(fileName, out var owner))
            {
                if (!ignoredOwners.Contains(owner))
                {
                    conflicts[fileName] = owner;
                }
            }
            // Check if the file exists on disk but is not managed (orphan file)
            else
            {
                var fullPath = Path.Combine(rootPath, fileName);
                if (File.Exists(fullPath))
                {
                    conflicts[fileName] = string.Empty;
                }
            }
        }

        return conflicts;
    }
}
