using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using OpenFileSystem.IO;
using OpenWrap.Extensions;
using OpenWrap.IO;
using OpenWrap.PackageManagement;
using OpenWrap.PackageModel;
using OpenWrap.PackageModel.Serialization;
using OpenWrap.Repositories;
using OpenWrap.Runtime;
using OpenWrap.Services;

namespace OpenWrap.Commands.Wrap
{
    [Command(Noun = "wrap", Verb = "init")]
    public class InitWrapCommand : AbstractCommand
    {
        const string MSBUILD_NS = "http://schemas.microsoft.com/developer/msbuild/2003";
        const string OPENWRAP_BUILD_CSHARP = @"wraps\openwrap\build\OpenWrap.CSharp.targets";
        const string OPENWRAP_BUILD_FSHARP = @"wraps\openwrap\build\OpenWrap.FSharp.targets";
        bool? _allProjects;
        string _name;
        IFile _packageDescriptorFile;
        IEnumerable<IFile> _projectsToPatch;

        public InitWrapCommand()
        {
            Target = ".";
        }

        [CommandInput]
        public bool All
        {
            get { return _allProjects ?? false; }
            set { _allProjects = value; }
        }

        [CommandInput]
        public bool Bazaar { get; set; }

        [CommandInput]
        public bool Git { get; set; }

        [CommandInput]
        public bool Hg { get; set; }

        [CommandInput]
        public string IgnoreFileName { get; set; }

        [CommandInput]
        public bool Meta { get; set; }

        [CommandInput(Position = 1)]
        public string Name
        {
            get { return _name ?? (Target == "." ? Environment.CurrentDirectory.Name : Target); }
            set { _name = value; }
        }

        [CommandInput]
        public string Projects { get; set; }

        [CommandInput]
        public bool Svn { get; set; }

        [CommandInput]
        public bool SymLinks { get; set; }

        [CommandInput(Position = 0)]
        public string Target { get; set; }

        IEnvironment Environment
        {
            get { return ServiceLocator.GetService<IEnvironment>(); }
        }

        IFileSystem FileSystem
        {
            get { return ServiceLocator.GetService<IFileSystem>(); }
        }

        public IEnumerable<ICommandOutput> VerifyArguments()
        {
            if (!All && string.IsNullOrEmpty(Projects))
                yield return new Warning("Specify either the path to the projects you want updated or -all to find all projects automatically.");
            if (All && !string.IsNullOrEmpty(Projects))
            {
                yield return new Warning(@"No project was specified. Either specify -all for all the projects in any folders under the current path, or -Project path\to\project.csproj.");
            }
            _projectsToPatch = All ? GetAllProjects() : GetSpecificProjects();
            Console.WriteLine("FooBar");
            _projectsToPatch.ToList().ForEach(x=> Console.WriteLine("==@@{0}", x));
            foreach (var proj in _projectsToPatch.Where(x => x.Exists == false))
                yield return new Warning("The project at path '{0}' does not exist. Check the path and try again.", proj.Path.FullPath);

            if (Git)
                IgnoreFileName = ".gitignore";
            if (Hg)
                IgnoreFileName = ".hgignore";
            if (Bazaar)
                IgnoreFileName = ".bzrignore";
            if (Svn)
            {
                IgnoreFileName = ".svnignore";
                SymLinks = true;
            }
        }

        protected override IEnumerable<ICommandOutput> ExecuteCore()
        {
            return SetupDirectoriesAndDescriptor()
                .Concat(ModifyProjects(Environment.DescriptorFile));
        }

        protected override IEnumerable<Func<IEnumerable<ICommandOutput>>> Validators()
        {
            yield return VerifyArguments;
        }

        static void AddPackageFolders(IDirectory projectDirectory)
        {
            projectDirectory.GetDirectory("src").MustExist();
            projectDirectory.GetDirectory("wraps").GetDirectory("_cache").MustExist();
        }

        void AddIgnores(IDirectory projectDirectory)
        {
            if (IgnoreFileName == null) return;
            if (Hg)
            {
                // mercurial is a bit special when it comes to ignores
                projectDirectory.GetFile(IgnoreFileName).WriteString("syntax: glob\r\nwraps/_cache");
            }
            else
            {
                projectDirectory.GetDirectory("wraps").GetFile(IgnoreFileName).WriteString("_cache\r\n_cache\\*");
            }
        }

        IEnumerable<ICommandOutput> CopyOpenWrap(PackageDescriptor projectDescriptor, IDirectory projectDirectory)
        {
            var packageManager = ServiceLocator.GetService<IPackageManager>();

            var repositoryOptions = FolderRepositoryOptions.AnchoringEnabled;
            if (projectDescriptor.UseSymLinks)
                repositoryOptions |= FolderRepositoryOptions.UseSymLinks;
            if (projectDescriptor.StorePackages)
                repositoryOptions |= FolderRepositoryOptions.PersistPackages;
            var projectRepository = new FolderRepository(projectDirectory.GetDirectory("wraps"), repositoryOptions)
            {
                Name = "Project repository"
            };
            packageManager.AddProjectPackage(PackageRequest.Any("openwrap"),
                                             new[] { Environment.SystemRepository },
                                             projectDescriptor,
                                             projectRepository,
                                             PackageAddOptions.Default | PackageAddOptions.Anchor | PackageAddOptions.Content).ToList();

            yield return new Info("Project repository initialized.");
        }

        IEnumerable<IFile> GetAllProjects()
        {
            return Environment.CurrentDirectory.Files("*.csproj", SearchScope.SubFolders)
                .Union(Environment.CurrentDirectory.Files("*.fsproj", SearchScope.SubFolders));
        }

        string GetOpenWrapPath(IDirectory projectPath, IDirectory rootPath, string buildTarget)
        {
            if (projectPath.Path == rootPath.Path) return buildTarget;
            int deepness = 1;

            for (var current = projectPath;
                 (current = current.Parent) != null;
                 deepness++)
            {
                if (current.Path == rootPath.Path)
                    return Enumerable.Repeat("..", deepness).JoinString("\\")
                           + "\\"
                           + buildTarget;
            }
            throw new InvalidOperationException("Could not find a descriptor.");
        }

        IEnumerable<IFile> GetSpecificProjects()
        {
            return string.IsNullOrEmpty(Projects)
                       ? Enumerable.Empty<IFile>()
                       : Projects.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(x => Environment.CurrentDirectory.GetFile(x));
        }

        IEnumerable<ICommandOutput> ModifyProjects(IFile descriptorFile)
        {
            descriptorFile = _packageDescriptorFile ?? descriptorFile;
          
            foreach (var project in _projectsToPatch)
            {
                if (project.Extension.Equals(".csproj", StringComparison.InvariantCultureIgnoreCase))
                    yield return ModifyCSharpProject(project, descriptorFile);

                if (project.Extension.Equals(".fsproj", StringComparison.InvariantCultureIgnoreCase))
                    yield return ModifyFSharpProject(project, descriptorFile);

               yield return new Info("Project '{0}' was not a recognized csharp or fsharp project file. Ignoring.", project.Name);
            }
        }

        private ICommandOutput ModifyFSharpProject(IFile project, IFile descriptorFile)
        {
            XmlNamespaceManager nsMgr;
            var xDoc = LoadXmlDocumentWithNamespace(project, out nsMgr);

            var fsharpTargets = (from node in xDoc.XPathSelectElements(@"//msbuild:Import", nsMgr)
                                 let attr = node.Attribute("Project").Value
                                 where attr != null && attr.EndsWith("Microsoft.FSharp.Targets")
                                 select node);

            var fsharpTarget = fsharpTargets.FirstOrDefault();
            if (fsharpTarget == null)
                return new Info("Project '{0}' was not a recognized fsharp project file. Ignoring.", project.Name);
         
            
            fsharpTarget.RemoveAll();
            fsharpTarget.SetAttributeValue("Project", 
                    GetOpenWrapPath(project.Parent, descriptorFile.Parent, OPENWRAP_BUILD_FSHARP));

            fsharpTargets.ToList()
                .ForEach(x=> x.Remove());

            SaveXmlToFile(xDoc, project);
          
            return new Info(string.Format("Project '{0}' updated to use OpenWrap.", project.Path.FullPath));
            
        }

        private ICommandOutput ModifyCSharpProject(IFile project, IFile descriptorFile)
        {
            XmlNamespaceManager nsMgr;
            var xDoc = LoadXmlDocumentWithNamespace(project, out nsMgr);

            var csharpTarget = (from node in xDoc.XPathSelectElements(@"//msbuild:Import", nsMgr)
                                let attr = node.Attribute("Project").Value
                                where attr != null && attr.EndsWith("Microsoft.CSharp.targets")
                                select node).FirstOrDefault();

            if (csharpTarget == null)
                return new Info("Project '{0}' was not a recognized csharp project file. Ignoring.", project.Name);
           
            // TODO: Detect path of openwrap directory and generate correct relative path from there
            csharpTarget.SetAttributeValue("Project", GetOpenWrapPath(project.Parent, descriptorFile.Parent, OPENWRAP_BUILD_CSHARP));
            SaveXmlToFile(xDoc, project);
            return new Info(string.Format("Project '{0}' updated to use OpenWrap.", project.Path.FullPath));
            
        }

        static void SaveXmlToFile(XElement xDoc, IFile file)
        {
            using (var stream = file.OpenWrite())
            using (var writer = XmlWriter.Create(stream))
            {
                xDoc.WriteTo(writer);
            }
        }


        private XElement LoadXmlDocumentWithNamespace(IFile file, out XmlNamespaceManager nsMgr)
        {
            using (var stream = file.OpenRead())
            using (var reader = XmlReader.Create(stream))
            {
                XElement root = XElement.Load(reader);
                XmlNameTable nameTable = reader.NameTable;

                nsMgr = new XmlNamespaceManager(nameTable);
                nsMgr.AddNamespace("msbuild", MSBUILD_NS);
                return root;
            }
          
        }

        IEnumerable<ICommandOutput> SetupDirectoriesAndDescriptor()
        {
            IDirectory currentDirectory = Environment.CurrentDirectory;

            IDirectory projectDirectory = Target == "." ? currentDirectory : currentDirectory.GetDirectory(Target);

            string packageName = Name;

            _packageDescriptorFile = projectDirectory.GetFile(packageName + ".wrapdesc");
            if (_packageDescriptorFile.Exists)
            {
                yield return new Info("Package descriptor present.");
                yield break;
            }

            var packageDescriptor = new PackageDescriptor { Name = packageName };
            if (SymLinks)
                packageDescriptor.UseSymLinks = true;
            if (Meta)
            {
                packageDescriptor.Build.Add("none");
            }
            else
            {
                AddPackageFolders(projectDirectory);
                AddIgnores(projectDirectory);
                foreach (var m in CopyOpenWrap(packageDescriptor, projectDirectory)) yield return m;
            }
            WriteVersionFile(projectDirectory);
            WriteDescriptor(_packageDescriptorFile, packageDescriptor);
            yield return new Info("Package '{0}' initialized. Start adding packages by using the 'add-wrap' command.", packageName);
        }

        void WriteDescriptor(IFile descriptor, PackageDescriptor packageDescriptor)
        {
            using (Stream descriptorStream = descriptor.OpenWrite())
            {
                new PackageDescriptorReaderWriter().Write(packageDescriptor, descriptorStream);
            }
        }

        void WriteVersionFile(IDirectory projectDirectory)
        {
            using (Stream versionFile = projectDirectory.GetFile("version").OpenWrite())
            {
                IO.StreamExtensions.Write(versionFile, Encoding.UTF8.GetBytes(("0.0.1.*")));
            }
        }
    }
}