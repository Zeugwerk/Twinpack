using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
            Projects = projectMatches.Cast<Match>()
                .Select(m => (match: m, projectPath: PathFromSolutionDir(directory, m.Groups[2].Value)))
                .Where(x => File.Exists(x.projectPath))
                .Select(x => new Project(x.match.Groups[1].Value, x.projectPath))
                .ToList();
        }

        public string Name { get; private set; } = null;
        public IEnumerable<Project> Projects { get; private set; } = new List<Project>();

        /// <summary>Combine solution directory with a path as stored in the .sln (may use Windows separators).</summary>
        private static string PathFromSolutionDir(string solutionDirectory, string relativeProjectPath)
        {
            if (string.IsNullOrEmpty(relativeProjectPath))
                return Path.GetFullPath(solutionDirectory);

            var rel = relativeProjectPath.Replace('\\', Path.DirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(solutionDirectory, rel));
        }
    }
}
