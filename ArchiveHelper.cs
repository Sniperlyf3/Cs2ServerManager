using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;

public static class ArchiveHelper
{
    /// <summary>
    /// Drop-in replacement for ZipFile.ExtractToDirectory(zip, target, overwrite)
    /// Supports both .zip and .rar using SharpCompress.
    /// </summary>
    public static void ExtractToDirectory(string archivePath, string targetDir, bool overwriteFiles = true)
    {
        string ext = Path.GetExtension(archivePath).ToLowerInvariant();

        if (ext == ".zip")
        {
            ZipFile.ExtractToDirectory(archivePath, targetDir, overwriteFiles);
        }
        else if (ext == ".rar")
        {
            Directory.CreateDirectory(targetDir);

            using (var archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        entry.WriteToDirectory(targetDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = overwriteFiles
                        });
                    }
                }
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported archive type: {ext}");
        }
    }
}
