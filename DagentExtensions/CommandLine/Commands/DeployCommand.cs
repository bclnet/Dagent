// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using DagentExtensions;
using Dagent.Configuration;
using Dagent.RemoteAgent;
using NuGet;
using NuGet.CommandLine;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Dagent.CommandLine
{
    [Command(typeof(Local), "deploy", "DeployCommandDescription", MinArgs = 2, MaxArgs = 3, UsageDescriptionResourceName = "DeployCommandUsageDescription", UsageSummaryResourceName = "DeployCommandUsageSummary", UsageExampleResourceName = "DeployCommandUsageExamples")]
    public class DeployCommand : Command
    {
        [ImportingConstructor]
        public DeployCommand(IBusDispacher busDispacher, IPackageAgentProvider packageRemoteProvider)
        {
            BusDispacher = busDispacher;
            RemoteProvider = packageRemoteProvider;
        }

        [Option(typeof(Local), "DeployCommandWaitDescription", AltName = "w")]
        public bool Wait { get; set; }

        [Option(typeof(Local), "DeployCommandIncludeDependencyDescription", AltName = "id")]
        public bool IncludeDependency { get; set; }

        [Option(typeof(Local), "DeployCommandApiKey")]
        public string ApiKey { get; set; }

        [Option(typeof(Local), "DeployCommandIncludeVersionDescription", AltName = "iv")]
        public bool IncludeVersion { get; set; }

        public IPackageAgentProvider RemoteProvider { get; }

        [Option(typeof(Local), "DeployCommandPrerelease")]
        public bool Prerelease { get; set; }

        [Option(typeof(Local), "DeployCommandVersionDescription")]
        public string Version { get; set; }

        [Option(typeof(Local), "DeployCommandEmailDescription")]
        public string Email { get; set; }

        [Option(typeof(Local), "DeployCommandProjectDescription")]
        public string Project { get; set; }

        protected IBusDispacher BusDispacher { get; set; }

        public override void ExecuteCommand()
        {
            var agent = Arguments[0];
            var packageId = Arguments[1];
            agent = RemoteProvider.ResolveAndValidateAgent(agent, out var defaultEmail);
            var remoteApiKey = GetRemoteApiKey(agent, true);
            if (string.IsNullOrEmpty(remoteApiKey))
                throw new CommandLineException(Local.NoAgentApiKeyFound, new[] { Utility.GetRemoteDisplayName(agent) });
            //if (!System.Messaging.MessageQueue.Exists(remoteQueue))
            //    throw new CommandLineException(Local.NoRemoteQueueFound, new object[] { remoteQueue });
            //
            var items = GetItemsFromPackage(packageId, out var fromSpec);
            if (items == null)
            {
                Console.WriteLine(Local.DeployCommandNoItemsFound, packageId);
                return;
            }
            DeployReply.WaitState waitState = null;
            if (Wait)
                waitState = new DeployReply.WaitState
                {
                    Success = x => Console.WriteLine(x),
                    Failure = () => Console.WriteLine($"Did not receive in time {agent}, {packageId}"),
                };
            BusDispacher.Send(agent, new DeployMessage
            {
                ApiKey = remoteApiKey,
                WantReply = Wait,
                FromSpec = fromSpec,
                Items = items,
                ExcludeVersion = !IncludeVersion,
                Prerelease = Prerelease,
                Email = Email ?? defaultEmail,
                Project = Project,
            });
            Console.WriteLine(Local.DeployCommandSent, agent, packageId);
            if (waitState != null)
            {
                Console.WriteLine("Waiting for reply...");
                waitState.DoWait();
            }
        }

        DeployMessage.Item[] GetItemsFromPackage(string packageId, out bool fromSpec)
        {
            if (Path.GetFileName(packageId).EndsWith(Constants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase))
            {
                if (IncludeDependency)
                    throw new CommandLineException("Message needed");
                fromSpec = true;
                Prerelease = true;
                return DeployPackagesFromConfigFile(GetPackageReferenceFile(packageId));
            }
            // single
            fromSpec = !IncludeDependency;
            return new[] { new DeployMessage.Item { PackageId = packageId, Version = Version } };
        }

        string GetRemoteApiKey(string remote, bool throwIfNotFound = true)
        {
            if (!string.IsNullOrEmpty(ApiKey))
                return ApiKey;
            string str = null;
            if (Arguments.Count > 2)
                str = Arguments[2];
            if (string.IsNullOrEmpty(str))
                str = Utility.GetRemoteApiKey(Settings, remote, throwIfNotFound);
            return str;
        }

        protected virtual PackageReferenceFile GetPackageReferenceFile(string path) { return new PackageReferenceFile(Path.GetFullPath(path)); }

        DeployMessage.Item[] DeployPackagesFromConfigFile(PackageReferenceFile file)
        {
            var references = file.GetPackageReferences().ToList();
            if (!references.Any())
                return null;
            var list = new List<DeployMessage.Item>();
            foreach (var reference in references)
            {
                if (string.IsNullOrEmpty(reference.Id))
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Local.DeployCommandInvalidPackageReference, new[] { Arguments[1] }));
                if (reference.Version == null)
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Local.DeployCommandPackageReferenceInvalidVersion, new[] { reference.Id }));
                list.Add(new DeployMessage.Item
                {
                    PackageId = reference.Id,
                    Version = reference.Version.ToString(),
                });
            }
            return list.ToArray();
        }
    }
}
