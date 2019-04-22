// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
//https://github.com/NuGet/NuGet.Client/tree/dev/src

using DagentExtensions;
using NuGet.CommandLine;
using NuGet.Configuration;
using System;

namespace Dagent
{
    internal static class Utility
    {
        public static readonly string ApiKeysSectionName = "apikeys";

        public static string GetRemoteApiKey(ISettings settings, string remote, bool throwIfNotFound = true)
        {
            var str = settings.GetValue(ApiKeysSectionName, remote);
            if (string.IsNullOrEmpty(str) && throwIfNotFound)
                throw new CommandLineException(Local.NoAgentApiKeyFound, new object[] { GetRemoteDisplayName(remote) });
            return str;
        }

        public static string GetRemoteDisplayName(string remote) => "'" + remote + "'";

        public static string EscapePSPath(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            path = path.Replace("[", "`[").Replace("]", "`]");
            return path.Contains("'") ? "\"" + path.Replace("$", "`$") + "\"" : "'" + path + "'";
        }

        public static bool IsValidAgent(string agent) { return NuGet.PathValidator.IsValidUrl(agent); }
    }
}
