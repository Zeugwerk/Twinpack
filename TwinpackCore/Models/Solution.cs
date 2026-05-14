using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Twinpack;

namespace Twinpack.Models
{
    public class Solution
    {
        public Solution()
        { 

        }

        public Solution(string name, List<Project> projects)
        {
            Name = name;
            Projects = projects;
        }

        public static Solution LoadFromFile(string filepath)
        {
            var solution = new Solution();
            solution.Load(filepath);
            return solution;
        }

        public void Load(string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"Solution {filepath} could not be found in workspace");

            var directory = new FileInfo(filepath).Directory.FullName;
            var content = File.ReadAllText(filepath);

            //Project("{DFBE7525-6864-4E62-8B2E-D530D69D9D96}") = "ZApplication", "ZApplication.tspproj", "{55567FAF-D581-431A-8E43-734906367EA7}"
            var projectMatches = Regex.Matches(content ?? "", $"Project\\(.*?\\)\\s*=\\s*\"(.*?)\"\\s*,\\s*\"(.*?ts[p]?proj)\"\\s*,.*");

            Name = Path.GetFileNameWithoutExtension(filepath);
            // The path captured from the .sln line is Windows-style ("ZApp\\ZApp.tspproj").
            // PathUtil.Combine converts the embedded '\\' to the platform separator so the
            // lookup also succeeds on Linux/macOS.
            Projects = projectMatches.Cast<Match>()
                .Select(x => new
                {
                    ProjectName = x.Groups[1].Value,
                    ProjectPath = PathUtil.Combine(directory, x.Groups[2].Value)
                })
                .Where(x => File.Exists(x.ProjectPath))
                .Select(x => new Project(x.ProjectName, x.ProjectPath))
                .ToList();
        }

        public string Name { get; private set; } = null;
        public IEnumerable<Project> Projects { get; private set; } = new List<Project>();
    }
}
