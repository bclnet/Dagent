// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagent.Configuration
{
    public static class PackageAgentProviderExtensions
    {
        public static string ResolveAndValidateAgent(this IPackageAgentProvider provider, string agent, out string defaultEmail)
        {
            if (string.IsNullOrEmpty(agent))
            {
                defaultEmail = null;
                return null;
            }
            agent = provider.ResolveAgent(agent, out defaultEmail);
            // Utility.ValidateAgent(agent);
            return agent;
        }

        public static string ResolveAgent(this IPackageAgentProvider provider, string value, out string defaultEmail)
        {
            var resolvedAgent = provider.GetEnabledPackageAgents()
                .Where(remote => remote.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) || remote.Agent.Equals(value, StringComparison.OrdinalIgnoreCase))
                .Select(remote => remote.Agent).FirstOrDefault();
            defaultEmail = null;
            return resolvedAgent ?? value;
        }

        public static IEnumerable<PackageAgent> GetEnabledPackageAgents(this IPackageAgentProvider provider) => provider.LoadPackageAgents().Where(p => p.IsEnabled);
    }
}
