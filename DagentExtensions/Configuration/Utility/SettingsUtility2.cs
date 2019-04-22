// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagent.Configuration
{
    public static class SettingsUtility2
    {
        public static IEnumerable<PackageAgent> GetEnabledAgents(ISettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var provider = new PackageAgentProvider(settings);
            return provider.LoadPackageAgents().Where(e => e.IsEnabled == true).ToList();
        }

        /// <summary>
        /// The DefaultDeployAgent can be:
        /// - An absolute URL
        /// - An absolute file path
        /// - A relative file path
        /// - The name of a registered agent from a config file
        /// </summary>
        public static string GetDefaultDeployAgent(ISettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var configSection = settings.GetSection(ConfigurationConstants.Config);
            var configSetting = configSection?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, ConfigurationConstants2.DefaultDeployAgent);

            var agent = configSetting?.Value;

            //var agentUri = UriUtility.TryCreateSourceUri(agent, UriKind.RelativeOrAbsolute);
            //if (agentUri != null && !agentUri.IsAbsoluteUri)
            //{
            //    // For non-absolute agents, it could be the name of a config agent, or a relative file path.
            //    var agentProvider = new PackageAgentProvider(settings);
            //    var allAgents = agentProvider.LoadPackageAgents();

            //    if (!allAgents.Any(s => s.IsEnabled && s.Name.Equals(agent, StringComparison.OrdinalIgnoreCase)))
            //        // It wasn't the name of a source, so treat it like a relative file 
            //        agent = Settings.ResolvePathFromOrigin(configSetting.Origin.DirectoryPath, configSetting.Origin.ConfigFilePath, agent);
            //}

            return agent;
        }
    }
}