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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
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
        public static LibraryInfo Read(byte[] libraryBinary)
        {
            var values = new List<string>();

            int ReadLength(BinaryReader reader)
            {
                byte lengthByte = reader.ReadByte();
                int length = lengthByte;
                if (length > 128) // check if last bit is set
                {
                    lengthByte = reader.ReadByte();
                    length = (length - 128) + lengthByte * 128;
                }

                return length;
            }

            using (var memoryStream = new MemoryStream(libraryBinary))
            using(var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
            {
                var stream = zipArchive.Entries.Where(x => x.Name == "__shared_data_storage_string_table__.auxiliary")?.FirstOrDefault().Open();

                try
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        int objects = ReadLength(reader);
                        var index = reader.ReadByte();
                        while (index < objects && index < 128)
                        {
                            var length = ReadLength(reader);
                            byte[] data = reader.ReadBytes(length);
                            string str = Encoding.UTF8.GetString(data);
                            values.Add(str);
                            
                            var nextIndex = reader.ReadByte();

                            // we reached the last string?
                            if (nextIndex - 1 != index)
                                break;

                            index = nextIndex;
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
