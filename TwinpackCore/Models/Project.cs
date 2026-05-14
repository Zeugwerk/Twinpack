using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Twinpack;

namespace Twinpack.Models
{
    public class Project
    {
        public Project(string name, List<Plc> plcs)
        {
            Name = name;
            Plcs = plcs;
        }
            
        public Project(string name, string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"Project {filepath} could not be found in workspace");

            var directory = new FileInfo(filepath).Directory.FullName;
            var content = XDocument.Load(filepath);

            Name = name;
            // PrjFilePath / File attributes inside .tsproj/.xti are Windows-style ("ZCore\\ZCore.plcproj");
            // PathUtil.Combine swaps the inner separators so the lookup also succeeds on Linux/macOS.
            Plcs = content?.Root?.Elements("Project")?.Elements("Plc")?.Elements("Project")
                .Where(x => x.Attribute("Name")?.Value != null && x.Attribute("PrjFilePath")?.Value != null)
                .Select(x => new Plc(x.Attribute("Name")?.Value, PathUtil.Combine(directory, x.Attribute("PrjFilePath")?.Value))).ToList();

            var xtis = content?.Root?.Elements("Project")?.Elements("Plc")?.Elements("Project")
                .Where(x => x.Attribute("File")?.Value != null)
                .Select(x => PathUtil.Combine(directory, "_Config", "PLC", x.Attribute("File")?.Value)).ToList();

            foreach(var xti in xtis)
            {
                if (!File.Exists(xti))
                    throw new FileNotFoundException($"XTI {xti} could not be found in workspace");

                content = XDocument.Load(xti);
                directory = new FileInfo(xti).Directory.FullName;

                Plcs = Plcs.Concat(content?.Root?.Elements("Project")?
                .Where(x => x.Attribute("Name")?.Value != null && x.Attribute("PrjFilePath")?.Value != null)
                .Select(x => new Plc(x.Attribute("Name")?.Value, PathUtil.Combine(directory, x.Attribute("PrjFilePath")?.Value)))).ToList();
            }
        }

        public string Name { get; private set; } = null;
        public List<Plc> Plcs { get; private set; } = new List<Plc>();
    }
}
