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

$thisPath = (Split-Path -parent $MyInvocation.MyCommand.Definition)
#$envDagentPathID = "DagentPath"
#$envDagentPackagesPathID = "DagentPackagesPath"
$defaultDagentPath = Join-Path ${env:ProgramFiles} "Dagent\"

# Environment Accessors
function Get-EnvPath { param ([string]$id) [Environment]::GetEnvironmentVariable($id, [EnvironmentVariableTarget]::Machine) }
function Set-EnvFilePath { param ([string]$id, [string]$path)
    if (!(Test-Path $path)) { mkdir $path | out-null }
	Write-Host "Setting `'$id`' as a Machine Environment variable to `'$path`'"
	[Environment]::SetEnvironmentVariable($id, $path, [EnvironmentVariableTarget]::Machine)
}
function Set-EnvSearchPath { param ([string]$id, [string]$path)
	$envPath = Get-EnvPath $id
	if ($envPath -and $envPath.ToLower().Contains($path.ToLower())) {
		Write-Host "Machine $id already contains `'$path`'"
		return
	}
	Write-Host "Setting `'$id`' as a Machine Environment variable. Adding to `'$path`'"
	$hasTerminator = $envPath -and $envPath.EndsWith(";")
	if (!$hasTerminator -and $envPath) { $path = ";" + $path }
	$envPath = $envPath + $path + ";"
	[Environment]::SetEnvironmentVariable($id, $envPath, [EnvironmentVariableTarget]::Machine)
	# change local session if PATH
	if ($envPath -eq "PATH") { $env:PATH = $env:PATH + ";" + $path + ";" }
}

# Installer
function Initialize-Dagent { param ([Parameter(Mandatory=$false)][string]$dagentPath = $defaultDagentPath, [bool]$force = $false)
    if (!(Test-Path $dagentPath)) { mkdir $dagentPath | out-null }
	Set-DagentFolderACL $dagentPath
    $dagentPackagesPath = Join-Path $dagentPath "Packages\"
    @"
Setting up Dagent.
"@ | Write-Host

	# adjust paths based on environment variable path, use it.
	$envDagentPath = Get-EnvPath "DagentPath"
    $envDagentPackagesPath = Get-EnvPath "DagentPackagesPath"
    if ($envDagentPath -and $envDagentPath -ne $dagentPath) { $dagentPath = $envDagentPath } else { Set-EnvFilePath "DagentPath" $dagentPath }
    if ($envDagentPackagesPath -and $envDagentPackagesPath -ne $dagentPackagesPath) { $dagentPackagesPath = $envDagentPackagesPath } else { Set-EnvFilePath "DagentPackagesPath" $dagentPackagesPath }
    @"
Application is located at `'$dagentPath`'.
The packages go to `'$dagentPackagesPath`'.
"@ | Write-Host

	# install
	Stop-DagentService $dagentPath
    Install-DagentFiles $dagentPath
	Create-DagentConfigFile $dagentPath $force
	Create-DagentPackagesPath $dagentPackagesPath
	Set-EnvSearchPath "PATH" $dagentPath
	Set-EnvSearchPath "NUGET_EXTENSIONS_PATH" (Join-Path $dagentPath "lib\")
	Install-DagentService $dagentPath $force
	@"
Install complete.
"@ | Write-Host
}

function Stop-DagentService { param ([string]$dagentPath)
	$hostExe = Join-Path $dagentPath "bin\Rhino.ServiceBus.Host.exe"
	if (Test-Path $hostExe) {
		Write-Host "Stopping existing service."
		Stop-Service Dagent
	}
}

# http://technet.microsoft.com/en-us/library/ff730951.aspx
function Set-DagentFolderACL { param ([string]$dagentPath)
	$colRights = [Security.AccessControl.FileSystemRights]"Modify"
	$objUser = new-object Security.Principal.NTAccount("NETWORK")
	$inheritanceFlags = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
	$propagationFlags = [Security.AccessControl.PropagationFlags]::None
	$objACE = new-object Security.AccessControl.FileSystemAccessRule($objUser, $colRights, $inheritanceFlags, $propagationFlags, [Security.AccessControl.AccessControlType]::Allow)
	$objACL = Get-ACL $dagentPath
	$objACL.AddAccessRule($objACE)
	Set-ACL $dagentPath $objACL
}

function Install-DagentFiles { param ([string]$dagentPath)
    if (!(Test-Path (Join-Path $thisPath "..\content"))) {
        Write-Host "ERROR@Install-DagentFiles: `'$thisPath`' not an install directory."
        return
    }
	#
    $installPath = Join-Path $thisPath "..\content\Dagent\*"
	$path = $dagentPath
    Write-Host "Copying the contents of `'$installPath`' to `'$path`'."
    Copy-Item $installPath $path -recurse -force
	#
    $installPath = Join-Path $thisPath "..\lib\net40\*"
	$path = Join-Path $dagentPath "bin\"
	if (!(Test-Path $path)) { mkdir $path | out-null }
    Write-Host "Copying the contents of `'$installPath`' to `'$path`'."
    Copy-Item $installPath $path -recurse -force
}

function Create-DagentPackagesPath { param ([string]$dagentPackagesPath)
	if (!(Test-Path $dagentPackagesPath)) { mkdir $dagentPackagesPath | out-null }
	$path = Join-Path $dagentPackagesPath "Readme.txt"
	"To add packages to the feed put package files (.nupkg files) in this folder." | Out-File $path -encoding ASCII
}

function Create-DagentConfigFile { param ([string]$dagentPath, [bool]$force)
    if (!(Test-Path (Join-Path $thisPath "..\content"))) {
        Write-Host "ERROR@Create-DagentConfigFile: `'$thisPath`' not an install directory."
        return
    }
    $configPath = Join-Path $thisPath "..\content\App.config"
	$path = Join-Path $dagentPath "bin\Contoso.Dagent.config"
	$markerFile = Join-Path $dagentPath "bin\marker_.ignore"
    if (!(Test-Path $markerFile) -or $force -eq $true) {
        Write-Host "Config contents at `'$configPath`' to `'$path`'."
        Copy-Item $configPath $path -force
        $s = [IO.File]::Open("$path", 'Open', 'Read', 'ReadWrite')
        $r = New-Object IO.StreamReader($s)
        $fileText = $r.ReadToEnd()
        $r.Close()
        $s.Close()
        #
        $fileText = $fileText.Replace("[:MachineName:]", [Environment]::MachineName).Replace("[:ApiKey:]", [Guid]::NewGuid())
        Set-Content $path -value $fileText -encoding ASCII
        Set-Content $markerFile -value "$([DateTime]::Now.Date)" -encoding ASCII
    } else { Write-Host "Keeping existing config contents at `'$path`'." }
}

function Install-DagentService { param ([string]$dagentPath, [bool]$force)
	$hostExe = Join-Path $dagentPath "bin\Rhino.ServiceBus.Host.exe"
	if (!(Test-Path $hostExe)) {
        Write-Host "ERROR@Install-DagentService: `'$hostExe`' not installed."
        return
    }
	if ((Test-Path $hostExe) -and $force -eq $true) { & $hostExe /action:Uninstall /asm:"Contoso.Dagent.dll" /name:"Dagent" }
	& $hostExe /action:Install /asm:"Contoso.Dagent.dll" /name:"Dagent" /config:"Contoso.Dagent.config"
	Write-Host "Starting service."
	Start-Service Dagent
}

export-modulemember -function Initialize-Dagent;