using CommandLine;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Commands
{
    [Verb("dump", HelpText = "")]
    public class DumpCommand : Command
    {
        [Option('p', "path", Required = false, Default = "Twinpack-Registry", HelpText = "")]
        public string Path { get; set; }

        public override async Task<int> ExecuteAsync()
        {
            _logger.Info(">>> twinpack-registry:dump");

            foreach (var file in Directory.GetFiles(Path))
            {
                try
                {
                    using (var memoryStream = new MemoryStream(File.ReadAllBytes(file)))
                    using (var zipArchive = new ZipArchive(memoryStream))
                    {
                        var libraryInfo = LibraryReader.Read(File.ReadAllBytes(file), dumpFilenamePrefix: file);
                    }
                }
                catch (Exception) { }
            }
            return 0;
        }
    }
}
