using Onova.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

namespace Unverum
{
    public class ZipExtractor : IPackageExtractor
    {
        public async Task ExtractPackageAsync(string sourceFilePath, string destDirPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (Path.GetExtension(sourceFilePath).Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                {
                    using (var archive = SevenZipArchive.Open(sourceFilePath))
                    {
                        var reader = archive.ExtractAllEntries();
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                                reader.WriteEntryToDirectory(destDirPath, new ExtractionOptions()
                                {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                        }
                    }
                }
                else
                {
                    using (Stream stream = File.OpenRead(sourceFilePath))
                    using (var reader = ReaderFactory.Open(stream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                reader.WriteEntryToDirectory(destDirPath, new ExtractionOptions()
                                {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                Global.logger.WriteLine("Failed to extract update", LoggerType.Error);
            }
            File.Delete(@$"{sourceFilePath}");
            // Move the folders to the right place
            string parentPath = Directory.GetParent(destDirPath).FullName;
            Directory.Move(Directory.GetDirectories(destDirPath)[0], $@"{parentPath}{Global.s}Unverum");
            Directory.Delete(destDirPath);
            Directory.Move($@"{parentPath}{Global.s}Unverum", destDirPath);
        }

    }
}
