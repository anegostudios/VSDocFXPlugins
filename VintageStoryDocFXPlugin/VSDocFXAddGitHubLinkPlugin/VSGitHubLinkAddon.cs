using Docfx.DataContracts.ManagedReference;
using Docfx.Plugins;
using System.Composition;
using System.Diagnostics;
using VintageStoryDocFXPlugin;

namespace VSDocFXAddGitHubLinkPlugin
{
    [Export(typeof(IDocumentProcessor))]
    public class VSGitHubLinkAddon : VSProcessorAddon
    {
        string codeFilesDir;
        static Dictionary<string, string>? typeNamespaceToFilepaths = null;

        //File path things for the github link.
        static Dictionary<string, string> filePathsToGitHubRepos = new Dictionary<string, string>
        {
            {"\\VintagestoryApi\\", "vsapi" },
            { "\\VSEssentials\\", "vsessentialsmod"},
            { "\\VSSurvivalMod\\", "vssurvivalmod" },
            { "\\VSCreativeMod\\", "vscreativemod" }
        };
        static string githubPrefix = "https://github.com/anegostudios/";
        static string githubMid = "/blob/master/";



        public VSGitHubLinkAddon() : base()
        {
            //Get the base path for all code files.
            //Current directory will give the CakeBuilder folder.
            codeFilesDir = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
            typeNamespaceToFilepaths = CreateClassNamespaceToFilePathDict();
        }

        public override void OnPageBeingProcessed(PageViewModel page)
        {
            string q = typeNamespaceToFilepaths.Keys.FirstOrDefault(x => x.Equals(page.Items[0].FullName, StringComparison.CurrentCultureIgnoreCase), null);

            if (q != null)
            {
                //Console.WriteLine("Class and namespace of " + qualName + " has a valid file path.");
                string githubPath = typeNamespaceToFilepaths[q].Replace(codeFilesDir, "", StringComparison.CurrentCultureIgnoreCase);

                string prefixTest = filePathsToGitHubRepos.Keys.FirstOrDefault(x => githubPath.Contains(x, StringComparison.CurrentCultureIgnoreCase), null);
                if (prefixTest != null)
                {
                    page.Items[0].Summary += " <a href=" + githubPrefix + filePathsToGitHubRepos[prefixTest] + githubMid + githubPath.Replace(prefixTest, "") + ">Open in GitHub</a>";
                }
            }
        }

        public Dictionary<string, string> CreateClassNamespaceToFilePathDict()
        {
            Console.WriteLine("Creating class + namespace to file path dictionary.");
            Dictionary<string, string> classToFile = new Dictionary<string, string>();

            int fCount = 0;
            int cFCount = 0;
            foreach (string s in Directory.GetFiles(codeFilesDir, "*.cs", SearchOption.AllDirectories))
            {
                //Thanks https://stackoverflow.com/a/19859067
                var code = File.ReadAllText(s);
                var classDeclarations = code
                     .Replace(Environment.NewLine, "")
                     .Split('{', '}')
                     .Where(c => c.Contains(" class ") || c.Contains("namespace ") || c.Contains("enum "));

                string foundNamespace = "";
                List<string> foundTypes = new List<string>();
                foreach (string c in classDeclarations)
                {
                    string[] words = c.Split(' ');
                    bool ns = false;
                    bool ty = false;
                    foreach (string word in words)
                    {
                        if (ns == true)
                        {
                            char c2 = word.FirstOrDefault(x => !char.IsLetterOrDigit(x) && x != '.' && x != '_', '}');
                            int len = word.IndexOf(c2);
                            if (len < 0)
                            {
                                len = word.Length;
                            }
                            foundNamespace = word.Substring(0, len);
                            ns = false;
                        }
                        else if (word.Contains("namespace", StringComparison.CurrentCultureIgnoreCase))
                        {
                            ns = true;
                        }
                        if (ty)
                        {
                            char c2 = word.FirstOrDefault(x => (x == '`') || (!char.IsLetterOrDigit(x) && x != '.' && x != '_'), '}');
                            int len = word.IndexOf(c2);
                            if (len < 0)
                            {
                                len = word.Length;
                            }
                            foundTypes.Add(word.Substring(0, len));
                            ty = false;
                        }
                        else if (word.Contains("class", StringComparison.CurrentCultureIgnoreCase) || word.Contains("enum", StringComparison.CurrentCultureIgnoreCase))
                        {
                            ty = true;
                        }
                    }
                }
                if (foundNamespace == "" && foundTypes.Count > 0)
                {
                    Console.WriteLine("Found types with no/empty namespace in file "+s+". These are unlikely to be included in the API docs, and will certainly not have a GitHub link found:");
                    foreach (string s2 in foundTypes)
                    {
                        Console.WriteLine(s2);
                    }
                }
                else
                {
                    if (foundTypes.Count > 0)
                    {
                        fCount++;
                    }
                    foreach (string ty in foundTypes)
                    {
                        if (!classToFile.ContainsKey(foundNamespace + "." + ty))
                        {
                            classToFile.Add(foundNamespace + "." + ty, s);
                        }
                    }
                }
            }
            Console.WriteLine("Finished creating dictionary. Found " + classToFile.Keys.Count + "type definitions in " + fCount + " files.");
            return classToFile;
        }

        public override string GetAddonName()
        {
            return "Github Link in Summary";
        }
    }
}
