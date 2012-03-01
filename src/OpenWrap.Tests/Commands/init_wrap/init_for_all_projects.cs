using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using OpenFileSystem.IO;
using OpenWrap.Testing;

namespace Tests.Commands.init_wrap
{
    public class init_for_all_projects : contexts.init_wrap
    {
        public init_for_all_projects()
        {
            given_csharp_project_file(@"project1.csproj");
            when_executing_command("-all");
        }
        [Test]
        public void command_succeeds()
        {
            Results.ShouldHaveNoError();
        }
        [Test]
        public void project_is_patched()
        {
            var projectFIle = Environment.CurrentDirectory.GetFile(@"project1.csproj");
            using (var stream = projectFIle.OpenRead())
            {
                var doc = XDocument.Load(new XmlTextReader(stream));

                doc.Document.Descendants(XName.Get("Import", MSBUILD_NS)).First().Attribute("Project").Value.ShouldContain("OpenWrap.CSharp.targets");
            }
        }
    }

    public class init_for_fsharp_projects : contexts.init_wrap
    {
        public init_for_fsharp_projects()
        {
            given_fsharp_project_file(@"project1.fsproj");
            when_executing_command("-all");
        }

          [Test]
        public void command_succeeds()
        {
            Results.ShouldHaveNoError();
        }
        [Test]
        public void project_is_patched()
        {
            var projFile = Environment.CurrentDirectory.GetFile(@"project1.fsproj");
            using (var stream = projFile.OpenRead())
            {
                Console.WriteLine("Openning: {0}", projFile.Path);
                var doc = XDocument.Load(new XmlTextReader(stream));
                Console.WriteLine("fsproject: {0}", doc.ToString());
                doc.Document.Descendants(XName.Get("Import", MSBUILD_NS)).First().Attribute("Project").Value.ShouldContain("OpenWrap.FSharp.targets");
            }
        }
    }
}