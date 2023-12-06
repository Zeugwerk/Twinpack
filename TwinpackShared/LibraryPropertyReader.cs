/*
 * Copyright (c) 2023 Andrew Burks
 * 
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 * 
 * The above copyright notice and this permission 
 * notice shall beincluded in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 */

using NLog;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Twinpack.Models;
using static Microsoft.VisualStudio.Shell.RegistrationAttribute;

namespace Twinpack
{
    public class LibraryPropertyReader
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
            public List<PlcLibrary> Dependencies { get; set; }
        }

        static List<string> _identifiers = new List<string> () { 
            "DefaultNamespace", "Project", "Company", "Title", "Description",
            "Author", "Version", "Placeholder", "Released" };

        const string ProjectInformationGuid = @"11c0fc3a-9bcf-4dd8-ac38-efb93363e521";
        const string LibraryManagerGuid = @"adb5cb65-8e1d-4a00-b70a-375ea27582f3";
        const string ProjectSettings = @"6470a90f-b7cb-43ac-9ae5-94b2338b4573";
        public static LibraryInfo Read(byte[] libraryBinary)
        {
            var values = new List<string>();

            using (var memoryStream = new MemoryStream(libraryBinary))
            using(var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
            {
                var stream = zipArchive.Entries.Where(x => x.Name == "__shared_data_storage_string_table__.auxiliary")?.FirstOrDefault().Open();
                byte index = 0;

                try
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var buffer = new List<byte>();

                        // read until we find index=0, which indicates we found the first string
                        while (true)
                        {
                            var b = reader.ReadByte();
                            if (b == 0)
                                break;

                            buffer.Add(b);
                        }

                        // now we know the number of strings and can iterate over them, storing them in a list of strings
                        // todo: buffer[0] is sufficent to get the data we need,
                        var objects = buffer[0] - 1;
                        long passedGuids = -1;
                        while (true)
                        {
                            byte length = reader.ReadByte();
                            string val = Encoding.ASCII.GetString(reader.ReadBytes(length));

                            // ugly heuristics to not have to read to the end 
                            if (string.Equals(val, ProjectInformationGuid, StringComparison.InvariantCultureIgnoreCase))
                                passedGuids = 0;

                            if (passedGuids >= 0 && Guid.TryParse(val, out _))
                                passedGuids++;

                            if (passedGuids > 10)
                                break;

                            if (string.Equals(val, ProjectSettings, StringComparison.InvariantCultureIgnoreCase))
                                break;

                            values.Add(val);
                            index = reader.ReadByte();
                        }

                    }
                }
                catch(Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Warn(ex.Message);
                }

            }

            var dependencies = values.Select(x => Regex.Match(x, @"^([A-Za-z].*?),\s*(.*?)\s*\(([A-Za-z].*)\)$"))
                                     .Where(x => x.Success)
                                     .Select(x => new PlcLibrary() { Name = x.Groups[1].Value, Version = x.Groups[2].Value, DistributorName = x.Groups[3].Value })
                                     .ToList();

            // yes, this is needed ... not sure how Codesys parses this stuff
            var title = Value(values, "Title");
            if(string.IsNullOrEmpty(title) || Guid.TryParse(title, out _) || _identifiers.Contains(title))
            {
                title = Value(values, "Placeholder");
                if (string.IsNullOrEmpty(title) || Guid.TryParse(title, out _) || _identifiers.Contains(title))
                {
                    title = Value(values, "DefaultNamespace");
                }
            }

            return new LibraryInfo()
            {
                DefaultNamespace = Value(values, "DefaultNamespace"),
                Company = Value(values, "Company"),
                Version = Value(values, "Version"),
                Title = title,
                Description = Value(values, "Description"),
                Author = Value(values, "Author"),
                Dependencies = dependencies
            };
        }

        private static string Value(List<string> values, string key)
        {
            var idx = values.FindIndex(x => string.Equals(x, key, StringComparison.CurrentCultureIgnoreCase));
            return idx < 0 ? "" : values[idx+1];
        }
    }
}
