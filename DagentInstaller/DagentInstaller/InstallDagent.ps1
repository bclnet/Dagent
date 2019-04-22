# ==============================================================================
# The MIT License
#
# Copyright (c) 2012 Sky Morey
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
# ==============================================================================

$installerUrl = "http://nuget.degdarwin.com/api/v2/package/dagent/1.0.0"
#$installerUrl = "file:///C:/T_/Packages/dagent.1.0.0.nupkg"
$tempPath = Join-Path $env:TEMP "dagent"
if (!(Test-Path $tempPath)) { [IO.Directory]::CreateDirectory($tempPath) }

# download the installer
$installerPath = Join-Path $tempPath "DagentInstaller.zip"
Write-Host "Downloading $installerUrl to $installerPath"
$downloader = new-object Net.WebClient
$downloader.DownloadFile($installerUrl, $installerPath)

# unzip the package
Write-Host "Extracting $installerPath to destination..."
$shellApplication = new-object -com shell.application 
$zipPackage = $shellApplication.NameSpace($installerPath) 
$destinationFolder = $shellApplication.NameSpace($tempPath) 
$destinationFolder.CopyHere($zipPackage.Items(),0x10)
$toolsPath = Join-Path $tempPath "tools"

# call Dagent installer
$dagentInstallerPS1 = Join-Path $toolsPath "DagentInstaller.ps1"
Write-Host "Executing $dagentInstallerPS1..."
& $dagentInstallerPS1

# ensure dagent::bin in session path
#Write-Host "Ensuring Dagent is included in the session path"
#$dagentPath = [Environment]::GetEnvironmentVariable("DagentPath", [EnvironmentVariableTarget]::Machine)
#if ($dagentPath -ne $null) { $dagentBinPath = Join-Path $dagentPath "bin" } else { $dagentBinPath = Join-Path ${env:ProgramFiles} "Dagent\bin" }
#if ($($env:Path).ToLower().Contains($($dagentBinPath).ToLower()) -eq $false) { $env:Path = $env:Path; }
