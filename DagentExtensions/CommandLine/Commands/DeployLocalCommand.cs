// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using DagentExtensions;
using NuGet;
using NuGet.CommandLine;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Dagent.CommandLine
{
    [Command(typeof(Local), "deployLocal", "DeployLocalCommandDescription", UsageDescriptionResourceName = "DeployLocalCommandUsageDescription", UsageSummaryResourceName = "DeployLocalCommandUsageSummary", UsageExampleResourceName = "DeployLocalCommandUsageExamples")]
    public class DeployLocalCommand : InstallCommand
    {
        #region base

        static MethodInfo _CalculateEffectivePackageSaveModeInfo = typeof(DownloadCommandBase).GetMethod("CalculateEffectivePackageSaveMode", BindingFlags.NonPublic | BindingFlags.Instance);
        static MethodInfo _CalculateEffectiveSettingsInfo = typeof(InstallCommand).GetMethod("CalculateEffectiveSettings", BindingFlags.NonPublic | BindingFlags.Instance);
        static MethodInfo _ResolveInstallPathInfo = typeof(InstallCommand).GetMethod("ResolveInstallPath", BindingFlags.NonPublic | BindingFlags.Instance);
        static MethodInfo _PrintPackageSourcesInfo = typeof(InstallCommand).Assembly.GetType("NuGet.CommandLine.ConsoleExtensions").GetMethod("PrintPackageSources", BindingFlags.Public | BindingFlags.Static);

        #endregion

        [ImportingConstructor]
        public DeployLocalCommand(IScriptExecutor scriptExecutor)
            : base()
        {
            ScriptExecutor = scriptExecutor;
            NoCache = true;
        }

        [Option(typeof(Local), "DeployLocalCommandProjectDescription")]
        public object Project { get; set; }

        protected IScriptExecutor ScriptExecutor { get; set; }

        public override Task ExecuteCommandAsync()
        {
            // On mono, parallel builds are broken for some reason. See https://gist.github.com/4201936 for the errors
            // That are thrown.
            DisableParallelProcessing |= RuntimeEnvironmentHelper.IsMono;

            if (DisableParallelProcessing)
                HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();

            _CalculateEffectivePackageSaveModeInfo.Invoke(this, null);
            _CalculateEffectiveSettingsInfo.Invoke(this, null);
            var installPath = (string)_ResolveInstallPathInfo.Invoke(this, null);

            var configFilePath = Path.GetFullPath(Arguments.Count == 0 ? Constants.PackageReferenceFile : Arguments[0]);
            var configFileName = Path.GetFileName(configFilePath);

            // If the first argument is a packages.xxx.config file, install everything it lists
            // Otherwise, treat the first argument as a package Id
            if (CommandLineUtility.IsValidConfigFileName(configFileName))
            {
                Prerelease = true;

                // display opt-out message if needed
                if (Console != null && RequireConsent &&
                    new NuGet.PackageManagement.PackageRestoreConsent(Settings).IsGranted)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Local.RestoreCommandPackageRestoreOptOutMessage,
                        NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                    Console.WriteLine(message);
                }

                return PerformV2RestoreAsync(configFilePath, installPath);
            }
            else
                throw new NotImplementedException();
        }

        async Task PerformV2RestoreAsync(string packagesConfigFilePath, string installPath)
        {
            EventHandler<PackageRestoredEventArgs> installedHandler = (sender, e) =>
            {
                if (!e.Restored)
                    return;
                var packagePath = Path.Combine(installPath, e.Package.ToString());
                try { ScriptExecutor.ExecuteInstallScript(packagePath, e.Package, Project, NuGet.NullLogger.Instance); }
                catch (Exception ex)
                {
                    Console.WriteLine("InstallScript: " + ex.Message);
                    Console.WriteLine();
                    Console.WriteLine(ex.StackTrace);
                }
            };

            var sourceRepositoryProvider = GetSourceRepositoryProvider();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, installPath, ExcludeVersion);

            var installedPackageReferences = GetInstalledPackageReferences(
                packagesConfigFilePath,
                allowDuplicatePackageIds: true);

            var packageRestoreData = installedPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { packagesConfigFilePath },
                    isMissing: true));

            var packageSources = GetPackageSources(Settings);

            _PrintPackageSourcesInfo.Invoke(null, new object[] { Console, packageSources });

            var failedEvents = new ConcurrentQueue<PackageRestoreFailedEventArgs>();

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: installedHandler,
                packageRestoreFailedEvent: (sender, args) => { failedEvents.Enqueue(args); },
                sourceRepositories: packageSources.Select(sourceRepositoryProvider.CreateRepository),
                maxNumberOfParallelTasks: DisableParallelProcessing ? 1 : PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            var missingPackageReferences = installedPackageReferences.Where(reference =>
                !nuGetPackageManager.PackageExistsInPackagesFolder(reference.PackageIdentity)).Any();

            if (!missingPackageReferences)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Local.InstallCommandNothingToInstall,
                    packagesConfigFilePath);

                Console.LogMinimal(message);
            }
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = NoCache;
                cacheContext.DirectDownload = DirectDownload;

                var clientPolicyContext = ClientPolicyContext.GetClientPolicy(Settings, Console);

                var projectContext = new ConsoleProjectContext(Console)
                {
                    PackageExtractionContext = new PackageExtractionContext(
                        NuGet.Packaging.PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        clientPolicyContext,
                        Console)
                };

                var downloadContext = new PackageDownloadContext(cacheContext, installPath, DirectDownload)
                {
                    ClientPolicyContext = clientPolicyContext
                };

                var result = await PackageRestoreManager.RestoreMissingPackagesAsync(
                    packageRestoreContext,
                    projectContext,
                    downloadContext);

                if (downloadContext.DirectDownload)
                    GetDownloadResultUtility.CleanUpDirectDownloads(downloadContext);

                // Use failure count to determine errors. result.Restored will be false for noop restores.
                if (failedEvents.Count > 0)
                {
                    // Log errors if they exist
                    foreach (var message in failedEvents.Select(e => new RestoreLogMessage(LogLevel.Error, NuGetLogCode.Undefined, e.Exception.Message)))
                        await Console.LogAsync(message);

                    throw new ExitCodeException(1);
                }
            }
        }

        CommandLineSourceRepositoryProvider GetSourceRepositoryProvider() => new CommandLineSourceRepositoryProvider(SourceProvider);
    }
}
