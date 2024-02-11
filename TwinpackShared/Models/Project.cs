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
    public class Project
    {
        public Project(string name, string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"Project {filepath} could not be found in workspace");

            var directory = new FileInfo(filepath).Directory.FullName;
            var content = XDocument.Load(filepath);

            Name = name;
            Plcs = content?.Root?.Elements("Project")?.Elements("Plc")?.Elements("Project")
                .Where(x => x.Attribute("Name")?.Value != null && x.Attribute("PrjFilePath")?.Value != null)
                .Select(x => new Plc(x.Attribute("Name")?.Value, $@"{directory}\{x.Attribute("PrjFilePath")?.Value}")).ToList();
        }

        public string Name { get; private set; } = null;
        public List<Plc> Plcs { get; private set; } = new List<Plc>();
    }
}
