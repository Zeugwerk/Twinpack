using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Twinpack.Models;

namespace Twinpack
{
    public class LibraryReader
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public class LibraryInfo
        {
            public string DefaultNamespace { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Author { get; set; }
            public string Company { get; set; }
            public string Version { get; set; }
            public List<PlcLibrary> Dependencies { get; set; } = new List<PlcLibrary> { };
        }

        enum PropertyType
        {
            Eof = 0x00,
            Boolean = 0x01,
            //Number = 0x03,
            //Date = ?,
            Text = 0x0E,
            Version = 0x0F,
            LibraryCategories = 0x81,
        }

        static int ReadLength(BinaryReader reader)
        {
            return ReadLength(reader, reader.ReadByte());
        }

        static int ReadLength(BinaryReader reader, byte currentByte)
        {
            int length = currentByte;
            if (length > 128) // check if last bit is set
            {
                currentByte = reader.ReadByte();
                length = (length - 128) + currentByte * 128;
            }

            return length;
        }

        public static List<string> ReadStringTable(ZipArchive archive, string dumpFilenamePrefix = null)
        {
            _logger.Trace("Reading string table");

            var stringTable = new List<string>();
            var stream = archive.Entries.Where(x => x.Name == "__shared_data_storage_string_table__.auxiliary")?.FirstOrDefault().Open();

            var objects = 0;
            var index = 0;
            try
            {
                using (var reader = new BinaryReader(stream))
                {
                    objects = ReadLength(reader);
                    _logger.Trace($"String table contains {objects} strings");
                    index = reader.ReadByte();
                    while (index < objects - 1 && index < 128)
                    {
                        var length = ReadLength(reader);
                        byte[] data = reader.ReadBytes(length);
                        string str = Encoding.UTF8.GetString(data);
                        stringTable.Add(str);

                        var nextIndex = reader.ReadByte();

                        // we reached the last string?
                        if (nextIndex - 1 != index)
                        {
                            break;
                        }

                        index = nextIndex;
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Warn(ex.Message);
            }


            if (dumpFilenamePrefix != null)
            {
                using (var streamWriter = new StreamWriter(dumpFilenamePrefix + ".stringtable"))
                {
                    index = 0;
                    foreach (var key in stringTable)
                    {
                        streamWriter.WriteLine($"{index.ToString("X")}: {key}");
                        index++;
                    }
                }
            }

            return stringTable;
        }

        public static LibraryInfo ReadProjectInformationXml(ZipArchive archive, List<string> stringTable, string dumpFilenamePrefix)
        {
            var entry = archive.Entries.Where(x => x.Name == "projectinformations.auxiliary")?.FirstOrDefault();
            if (entry == null)
                return null;

            _logger.Trace("Reading Project Information from xml");

            var stream = entry.Open();
            if (dumpFilenamePrefix != null)
            {
                _logger.Trace($"Dumping project information file {entry.Name}");
                using (var fileStream = File.Create(dumpFilenamePrefix + ".projectinfo.xml"))
                    stream.CopyTo(fileStream);

                stream = entry.Open();
            }

            var xdoc = XDocument.Load(stream);
            var properties = xdoc.Root.Element("Properties") ?? xdoc.Root.Element("properties");
            if (properties == null)
                return null;

            return new LibraryInfo()
            {
                Company = properties?.Element("Company")?.Value ?? "",
                DefaultNamespace = properties?.Element("DefaultNamespace")?.Value ?? "",
                Version = properties?.Element("Version")?.Value ?? "",
                Title = properties?.Element("Title")?.Value ?? "",
                Author = properties?.Element("Author")?.Value ?? "",
                Description = properties?.Element("Description")?.Value ?? "",
            };
        }

        public static LibraryInfo ReadProjectInformationBin(ZipArchive archive, List<string> stringTable, string dumpFilenamePrefix)
        {
            var entry = archive.Entries.Where(x => x.Name == "11c0fc3a-9bcf-4dd8-ac38-efb93363e521.object")?.FirstOrDefault();
            if (entry == null)
                return null;

            _logger.Trace("Reading Project Information from binary");

            var stream = entry.Open();
            if (dumpFilenamePrefix != null)
            {
                _logger.Trace($"Dumping project information file {entry.Name}");
                using (var fileStream = File.Create(dumpFilenamePrefix + ".projectinfo.bin"))
                    stream.CopyTo(fileStream);

                stream = entry.Open();
            }

            var properties = new Dictionary<string, string>();
            using (var reader = new BinaryReader(stream))
            {

                var header = reader.ReadBytes(16);
                _logger.Trace($"Read header " + BitConverter.ToString(header));

                var length = ReadLength(reader);
                _logger.Trace($"Payload contains {length} bytes");

                var sectionHeader = reader.ReadBytes(7);
                _logger.Trace($"Read section " + BitConverter.ToString(header));

                using (var libcatWriter = dumpFilenamePrefix == null ? null : new StreamWriter(dumpFilenamePrefix + ".libcat"))
                {
                    while (true)
                    {
                        var nameIdx = reader.ReadByte();
                        var name = stringTable[nameIdx];
                        var type = reader.ReadByte();

                        if ((PropertyType)type == PropertyType.Eof)
                        {
                            _logger.Trace("EOF");
                            break;
                        }

                        _logger.Trace($"Reading property '{name}' (type: {(Enum.IsDefined(typeof(PropertyType), (Int32)type) ? ((PropertyType)type).ToString() : type.ToString())})");
                        switch ((PropertyType)type)
                        {
                            case PropertyType.Boolean:
                                properties[name] = (reader.ReadByte() > 0).ToString();
                                break;
                            //case PropertyType.Number:
                            //    properties[name] = reader.ReadUInt16().ToString();
                            //    break;
                            case PropertyType.Text:
                                properties[name] = stringTable[reader.ReadByte()];
                                break;
                            case PropertyType.Version:
                                var parts = reader.ReadByte(); // not sure why this is needed from codesys
                                properties[name] = stringTable[reader.ReadByte()];
                                break;
                            case PropertyType.LibraryCategories:
                                var guid = stringTable[reader.ReadByte()]; // For LibraryCategories this is System.Guid
                                var count = reader.ReadByte();

                                libcatWriter?.WriteLine("GUID:" + guid);
                                libcatWriter?.WriteLine("Library Categories:" + count);

                                // this is weird, but kinda works ...
                                if (guid == "System.Guid")
                                {
                                    var pad2 = reader.ReadBytes(2);
                                    var libCatGuidIndices = reader.ReadBytes(count); // sequence of guids to the libcats

                                    libcatWriter?.WriteLine($"{pad2[0].ToString("X")}");
                                    libcatWriter?.WriteLine($"{pad2[1].ToString("X")} - {stringTable[pad2[1]]}");
                                    for (var i = 0; i < count; ++i)
                                    {
                                        libcatWriter?.WriteLine($"GUID: {stringTable[libCatGuidIndices[i]]}");
                                    }
                                    libcatWriter?.WriteLine("---------------------------------------------------");
                                }
                                else
                                {
                                    // there is some header - no idea what it doesn, but it contains 2 guids
                                    var pad2 = reader.ReadBytes(4);

                                    // the next bytes contain the following information for every library category
                                    // only the last one is special, it has 3 bytes less
                                    // - default name
                                    // - some guid, probably related to the library catalog
                                    // - some number
                                    // - version
                                    // - some number
                                    // - guidn - guid that is related to some kind of chaining, probably a linked list
                                    // - guidp - same here
                                    var pad3 = reader.ReadBytes(7 * (count-1) + 4);

                                    libcatWriter?.WriteLine($"{pad2[0].ToString("X")}");
                                    libcatWriter?.WriteLine($"{pad2[1].ToString("X")}");
                                    libcatWriter?.WriteLine($"{pad2[2].ToString("X")} - {stringTable[pad2[2]]}");
                                    libcatWriter?.WriteLine($"{pad2[3].ToString("X")} - {stringTable[pad2[3]]}");

                                    for (var j = 0; j < count; ++j)
                                    {
                                        libcatWriter?.WriteLine($"== {j} ==");
                                        libcatWriter?.WriteLine($"DefaultName: {stringTable[pad3[(j * 7) + 0]]}");
                                        libcatWriter?.WriteLine($"{pad3[(j * 7) + 1].ToString("X")} - {stringTable[pad3[(j * 7) + 1]]}");
                                        libcatWriter?.WriteLine($"{pad3[(j * 7) + 2].ToString("X")}");
                                        libcatWriter?.WriteLine($"Version: {stringTable[pad3[(j * 7) + 3]]}");

                                        if (j < count - 1)
                                        {
                                            libcatWriter?.WriteLine($"{pad3[(j * 7) + 4].ToString("X")}");
                                            libcatWriter?.WriteLine($"Id1: {stringTable[pad3[(j * 7) + 5]]}");
                                            libcatWriter?.WriteLine($"Id2: {pad3[(j * 7) + 6].ToString("X")} - {stringTable[pad3[(j * 7) + 6]]}");
                                        }
                                    }

                                    libcatWriter?.WriteLine("---------------------------------------------------");
                                }

                                break;
                        }
                    }
                }
            }

            // not needed anymore
            if (!properties.TryGetValue("Title", out string v) || string.IsNullOrEmpty(v))
                _logger.Warn("Title was not parsed correctly");

            return new LibraryInfo()
            {
                Company = properties.TryGetValue("Company", out v) ? v : "",
                DefaultNamespace = properties.TryGetValue("DefaultNamespace", out v) ? v : "",
                Version = properties.TryGetValue("Version", out v) ? v : "",
                Title = properties.TryGetValue("Title", out v) ? v : (properties.TryGetValue("Placeholder", out v) ? v : ""),
                Author = properties.TryGetValue("Author", out v) ? v : "",
                Description = properties.TryGetValue("Description", out v) ? v : "",
            };
        }

        public static LibraryInfo Read(byte[] libraryBinary, string dumpFilenamePrefix = null)
        {
            using (var memoryStream = new MemoryStream(libraryBinary))
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
            {
                var stringTable = ReadStringTable(zipArchive, dumpFilenamePrefix);
                var libraryInfo = ReadProjectInformationXml(zipArchive, stringTable, dumpFilenamePrefix) ??
                                  ReadProjectInformationBin(zipArchive, stringTable, dumpFilenamePrefix);

                if (libraryInfo == null)
                    throw new Exceptions.LibraryFileInvalidException("Fileformat is not supported, project information could not be extracted");

                libraryInfo.Dependencies = stringTable.Select(x => Regex.Match(x, @"^([A-Za-z].*?),\s*(.*?)\s*\(([A-Za-z].*)\)$"))
                                        .Where(x => x.Success)
                                        .Select(x => new PlcLibrary() { Name = x.Groups[1].Value, Version = x.Groups[2].Value, DistributorName = x.Groups[3].Value })
                                        .ToList();

                return libraryInfo;
            }
        }
    }
}
