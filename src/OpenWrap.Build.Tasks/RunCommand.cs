﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using OpenWrap.Commands;
using OpenWrap.Commands.Cli;
using OpenWrap.PackageManagement;
using OpenWrap.Runtime;
using OpenWrap.Services;

namespace OpenWrap.Build.Tasks
{
    public class RunCommand : Task
    {
        readonly IEnvironment _environment;
        readonly IPackageManager _packageManager;
        bool _success = true;

        public RunCommand()
            : this(ServiceLocator.GetService<IPackageManager>(),
                   ServiceLocator.GetService<IEnvironment>())
        {
        }

        public RunCommand(IPackageManager packageManager, IEnvironment environment)
        {
            _packageManager = packageManager;
            _environment = environment;
        }


        public string Args { get; set; }

        public string CurrentDirectory { get; set; }
        public bool Debug { get; set; }

        [Required]
        public string Noun { get; set; }

        [Required]
        public string Verb { get; set; }

        public override bool Execute()
        {
            if (Debug) Debugger.Launch();

            var runner = new CommandLineRunner();

            var commands = ReadCommands(_environment);

            var command = commands.FirstOrDefault(x => x.Noun.EqualsNoCase(Noun) && x.Verb.EqualsNoCase(Verb));
            if (command == null)
            {
                Log.LogError("Command named '{0}-{1}' is not a recognized command.", Verb, Noun);
                return false;
            }
            foreach (var value in runner.Run(command, GetArguments()))
                ProcessOutput(value);

            return _success;
        }

        static string EncodeQuotes(string value)
        {
            return value.Replace("\"", "`\"");
        }

        string GetArguments()
        {
            var xmlDoc = "<parameters>" + Args + "</parameters>";
// ReSharper disable PossibleNullReferenceException
            return (from child in XDocument.Parse(xmlDoc).Root.Descendants()
// ReSharper restore PossibleNullReferenceException
                    let key = "-" + child.Name.LocalName
                    let value = child.IsEmpty ? string.Empty : " \"" + EncodeQuotes(child.Value) + "\""
                    select key + value).JoinString(" ");
        }

        void ProcessOutput(ICommandOutput cmd)
        {
            switch (cmd.Type)
            {
                case CommandResultType.Error:
                    Log.LogError(cmd.ToString());
                    _success = false;
                    break;
                case CommandResultType.Warning:
                    Log.LogWarning(cmd.ToString());
                    break;
                case CommandResultType.Data:
                case CommandResultType.Info:
                    Log.LogMessage(cmd.ToString());
                    break;
                case CommandResultType.Verbose:
                    Log.LogMessage(MessageImportance.Low, cmd.ToString());
                    break;
            }
        }

        IEnumerable<ICommandDescriptor> ReadCommands(IEnvironment environment)
        {
            return _packageManager.Commands(environment);
        }
    }
}
