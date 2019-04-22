// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using NuGet.Packaging.Core;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Dagent
{
    [Export(typeof(IScriptExecutor))]
    public class ScriptExecutor : IScriptExecutor
    {
        static readonly object _lock = new object();

        public void ExecuteInstallScript(string installPath, PackageIdentity package, object project, ILogger logger)
        {
            lock (_lock)
                try
                {
                    var fullPath = Path.Combine(installPath, "tools", "install.cmd");
                    if (File.Exists(fullPath))
                    {
                        logger.Log(MessageLevel.Info, $"ExecutingScript {fullPath}");
                        var toolsPath = Path.GetDirectoryName(fullPath);
                        Environment.CurrentDirectory = toolsPath;
                        RunCmd(fullPath, new object[] { installPath, toolsPath, package.Id, project });
                        return;
                    }
                    fullPath = Path.Combine(installPath, "tools", "install.ps1");
                    if (File.Exists(fullPath))
                    {
                        logger.Log(MessageLevel.Info, string.Format(CultureInfo.CurrentCulture, "ExecutingScript {0}", fullPath));
                        var toolsPath = Path.GetDirectoryName(fullPath);
                        Environment.CurrentDirectory = toolsPath;
                        RunPs1(fullPath, new object[] { installPath, toolsPath, package, project });
                        return;
                    }
                }
                finally { Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location); }
        }

        public void ExecuteUninstallScript(string installPath, PackageIdentity package, object project, ILogger logger)
        {
            lock (_lock)
                try
                {
                    var fullPath = Path.Combine(installPath, "tools", "uninstall.cmd");
                    if (File.Exists(fullPath))
                    {
                        logger.Log(MessageLevel.Info, string.Format(CultureInfo.CurrentCulture, "ExecutingScript {0}", fullPath));
                        var toolsPath = Path.GetDirectoryName(fullPath);
                        Environment.CurrentDirectory = toolsPath;
                        RunCmd(fullPath, new object[] { installPath, toolsPath, package.Id, project });
                        return;
                    }
                    fullPath = Path.Combine(installPath, "tools", "uninstall.ps1");
                    if (File.Exists(fullPath))
                    {
                        logger.Log(MessageLevel.Info, string.Format(CultureInfo.CurrentCulture, "ExecutingScript {0}", fullPath));
                        var toolsPath = Path.GetDirectoryName(fullPath);
                        Environment.CurrentDirectory = toolsPath;
                        RunPs1(fullPath, new object[] { installPath, toolsPath, package, project });
                        return;
                    }
                }
                finally { Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location); }
        }

        void RunCmd(string fullPath, object[] args) =>
            RunProcess("cmd.exe", $"/C \"{fullPath}\" \"{args[0]}\" \"{args[1]}\" \"{args[2]}\"", 5 * 60 * 1000);

        void RunPs1(string fullPath, object[] args)
        {
            var cmd = $"-NonInteractive -ExecutionPolicy Unrestricted -File \"{fullPath}\" \"{args[0]}\" \"{args[1]}\" \"{args[2]}\"";
            Console.WriteLine("powershell.exe " + cmd);
            RunProcess("powershell.exe", cmd, 1 * 60 * 1000);
        }

        void RunProcess(string fileName, string arguments, int timeoutMilliseconds)
        {
            Thread threadToKill = null;
            Action action = () =>
            {
                threadToKill = Thread.CurrentThread;
                using (var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                })
                {
                    p.Start();
                    Console.WriteLine(p.StandardOutput.ReadToEnd());
                    p.WaitForExit();
                    var errors = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(errors))
                        Console.WriteLine(errors);
                }
            };
            var result = action.BeginInvoke(null, null);
            if (result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                action.EndInvoke(result);
                return;
            }
            threadToKill.Abort();
            throw new TimeoutException();
        }
    }
}
