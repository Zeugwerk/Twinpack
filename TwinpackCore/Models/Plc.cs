using System.IO;

namespace Twinpack.Models
{
    public class Plc
    {
        public Plc(string name, string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"PLC {filepath} could not be found in workspace");

            Name = name;
            FilePath = Path.GetFullPath(filepath);
        }

        public string Name { get; private set; } = null;
        public string FilePath { get; private set; } = null;
    }
}
