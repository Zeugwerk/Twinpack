using Microsoft.Internal.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Twinpack.Models
{
    public class Plc
    {
        public Plc(string name, string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"PLC {filepath} could not be found in workspace");

            Name = name;
            FilePath = filepath;
        }

        public string Name { get; private set; } = null;
        public string FilePath { get; private set; } = null;
    }
}
