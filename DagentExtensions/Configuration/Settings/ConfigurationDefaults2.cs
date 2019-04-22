// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace Dagent.Configuration
{
    public class ConfigurationDefaults2
    {
        ISettings _settingsManager = NullSettings.Instance;
        bool _defaultPackageAgentInitialized;
        List<PackageAgent> _defaultPackageAgents;
        string _defaultDeployAgent;

        static ConfigurationDefaults2 InitializeInstance()
        {
            var machineWideSettingsDir = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory);
            return new ConfigurationDefaults2(machineWideSettingsDir, ConfigurationConstants.ConfigurationDefaultsFile);
        }

        /// <summary>
        /// An internal constructor MAINLY INTENDED FOR TESTING THE CLASS. But, the product code is only expected to
        /// use the static Instance property
        /// Only catches FileNotFoundException. Will throw all exceptions including other IOExceptions and
        /// XmlExceptions for invalid xml and so on
        /// </summary>
        /// <param name="directory">The directory that has the NuGetDefaults.Config</param>
        /// <param name="configFile">Name of the NuGetDefaults.Config</param>
        internal ConfigurationDefaults2(string directory, string configFile)
        {
            try
            {
                if (File.Exists(Path.Combine(directory, configFile)))
                    _settingsManager = new Settings(directory, configFile);
            }
            catch (FileNotFoundException) { }
            // Intentionally, we don't catch all IOExceptions, XmlException or other file related exceptions like UnAuthorizedAccessException
            // This way, administrator will become aware of the failures when the ConfigurationDefaults file is not valid or permissions are not set properly
        }

        public static ConfigurationDefaults2 Instance { get; } = InitializeInstance();

        public IEnumerable<PackageAgent> DefaultPackageAgents
        {
            get
            {
                if (_defaultPackageAgents == null)
                {
                    _defaultPackageAgents = new List<PackageAgent>();
                    var disabledPackageAgents = _settingsManager.GetSection(ConfigurationConstants2.DisabledPackageAgents)?.Items.OfType<AddItem>() ?? Enumerable.Empty<AddItem>();
                    var packageAgents = _settingsManager.GetSection(ConfigurationConstants2.PackageAgents)?.Items.OfType<AgentItem>() ?? Enumerable.Empty<AgentItem>();

                    foreach (var agent in packageAgents)
                        // In a SettingValue representing a package source, the Key represents the name of the package source and the Value its source
                        _defaultPackageAgents.Add(new PackageAgent(agent.GetValueAsPath(),
                            agent.Key,
                            null,
                            isEnabled: !disabledPackageAgents.Any(p => p.Key.Equals(agent.Key, StringComparison.CurrentCultureIgnoreCase)),
                            isOfficial: true));
                }
                return _defaultPackageAgents;
            }
        }

        public string DefaultDeployAgent
        {
            get
            {
                if (_defaultDeployAgent == null && !_defaultPackageAgentInitialized)
                {
                    _defaultPackageAgentInitialized = true;
                    _defaultDeployAgent = SettingsUtility2.GetDefaultDeployAgent(_settingsManager);
                }
                return _defaultDeployAgent;
            }
        }

        public string DefaultPackageRestoreConsent => SettingsUtility.GetValueForAddItem(_settingsManager, ConfigurationConstants.PackageRestore, ConfigurationConstants.Enabled);
    }
}