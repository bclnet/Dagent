// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using NuGet.Packaging.Core;

namespace Dagent
{
    public interface IScriptExecutor
    {
        void ExecuteInstallScript(string installPath, PackageIdentity package, object project, ILogger logger);
        void ExecuteUninstallScript(string installPath, PackageIdentity package, object project, ILogger logger);
    }
}
