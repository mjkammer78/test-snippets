using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Codemanagement.Test
{
    public static class UnusedSourceFilesFinder
    {
        public static IEnumerable<string> EnumerateSuspectedUnusedSourceFilesInSolution(string baseDir, string solutionFileName)
        {
            var projects = EnumerateProjectsWithinSolution(baseDir, solutionFileName);
            var files = projects.SelectMany(p =>
                EnumerateSuspectedUnusedSourceFiles(Path.GetDirectoryName(p), Path.GetFileName(p)));

            return files;
        }

        public static IEnumerable<string> EnumerateProjectsWithinSolution(string baseDir, string solutionFilename)
        {
            var listOfProjects = new List<string>();

            using (var sr = File.OpenText(Path.Combine(baseDir, solutionFilename)))
            {
                const string
                    matchProjectNameRegex =
                        "^Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)\\s*=\\s* \"(?<PROJECTNAME>.*)\"\\s*,\\s*\"(?<PROJECTRELATIVEPATH>.*)\"\\s*,\\s*\"(?<PROJECTGUID>.*)\"$";

                string lineText;
                while ((lineText = sr.ReadLine()) != null)
                {
                    if (lineText.StartsWith("Project(", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var projectNameMatch = Regex.Match(lineText,
                            matchProjectNameRegex, RegexOptions.IgnoreCase);
                        if (projectNameMatch.Success)
                        {
                            listOfProjects.Add(projectNameMatch.Groups["PROJECTRELATIVEPATH"].Value);
                        }
                    }
                }

                sr.Close();
            }

            return listOfProjects
                .Where(p => Regex.IsMatch(p, @"\.csproj", RegexOptions.IgnoreCase))
                .Select(p => $@"{baseDir}\{p}");
        }

        private static IEnumerable<string> EnumerateSuspectedUnusedSourceFiles(string baseDir, string projectFilename)
        {
            return EnumerateFilesWithinProjectFolder(baseDir).Except(
                EnumerateIncludesWithinProject(baseDir, projectFilename));
        }

        private static IEnumerable<string> EnumerateFilesWithinProjectFolder(string baseDir)
        {
            return Directory.GetFiles(baseDir, "*.cs", SearchOption.AllDirectories)
                .Where(listEntry => !Regex.IsMatch(listEntry, @"\\obj\\|\\bin\\", RegexOptions.IgnoreCase));
        }

        private static IEnumerable<string> EnumerateIncludesWithinProject(string baseDir, string projectFilename)
        {
            XNamespace _msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            var projDefinition = XDocument.Load(Path.Combine(baseDir, projectFilename));
            var references = projDefinition
                .Element(_msbuild + "Project")
                .Elements(_msbuild + "ItemGroup")
                .Elements(_msbuild + "Compile")
                .Attributes("Include")
                .Select(refElem => refElem.Value);

            return references.Select(reference => $@"{baseDir}\{reference}");
        }
    }
}
