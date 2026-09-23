[CmdletBinding()]
param(
    [string]$SimHubPath = "${env:ProgramFiles(x86)}\SimHub",
    [switch]$Install
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\plugin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$dll = Join-Path $out 'DIYAmbient.Plugin.dll'
$staged = Join-Path $out 'staging'
# Clear only generated LOCAL outputs, never the installed SimHub DLL.
foreach ($name in @('DIYAmbient.Plugin.dll', 'DIYAmbient.Plugin.pdb', 'build-manifest.json')) {
    Remove-Item -LiteralPath (Join-Path $out $name) -Force -ErrorAction SilentlyContinue
}
if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged -Recurse -Force }
New-Item -ItemType Directory -Path $staged | Out-Null
& (Join-Path $PSScriptRoot 'Test.ps1')
if (-not $PSBoundParameters.ContainsKey('SimHubPath') -and
    -not (Test-Path -LiteralPath (Join-Path $SimHubPath 'SimHub.Plugins.dll'))) {
    $alternate = Join-Path $env:ProgramFiles 'SimHub'
    if (Test-Path -LiteralPath (Join-Path $alternate 'SimHub.Plugins.dll')) { $SimHubPath = $alternate }
}
foreach ($name in @('GameReaderCommon.dll', 'SimHub.Plugins.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SimHubPath $name) -PathType Leaf)) {
        throw "SDK SimHub reel introuvable ($name). Utiliser -SimHubPath 'D:\SimHub'."
    }
}
$SimHubPath = (Resolve-Path -LiteralPath $SimHubPath).Path
$framework = Get-FrameworkDirectory
$csc = Join-Path $framework 'csc.exe'
$stagedDll = Join-Path $staged 'DIYAmbient.Plugin.dll'
$lines = @('/nologo', '/target:library', '/platform:anycpu', '/langversion:5', '/warn:4', '/optimize+', '/codepage:65001', "/out:`"$stagedDll`"")
$lines += Get-ReferenceArguments $framework @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll', 'System.Xaml.dll', 'WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll')
$lines += Get-ReferenceArguments $framework @('System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
foreach ($name in @('Adalight_WS2812.ino')) {
    $firmwareSource = Join-Path $root "firmware\Adalight_WS2812\$name"
    $lines += "/resource:`"$firmwareSource`",DIYAmbient.Firmware.$name"
}
$hostAssemblies = @()
foreach ($name in @('GameReaderCommon.dll', 'SimHub.Plugins.dll', 'InputManagerCS.dll', 'SimHub.Logging.dll', 'log4net.dll', 'MahApps.Metro.dll', 'Newtonsoft.Json.dll')) {
    $file = Join-Path $SimHubPath $name
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        $lines += "/reference:`"$file`""
        $hostAssemblies += @{ name=$name; version=(Get-Item $file).VersionInfo.FileVersion; sha256=(Get-FileHash $file -Algorithm SHA256).Hash }
    }
}
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' -Recurse | Sort-Object FullName)
$lines += $sources | ForEach-Object { '"' + $_.FullName + '"' }
$rsp = Join-Path $out 'plugin.rsp'
$lines | Set-Content -LiteralPath $rsp -Encoding UTF8
Invoke-LoggedNative -Executable $csc -Arguments @("@$rsp") -LogPath (Join-Path $out 'compile.log')
if (-not (Test-Path -LiteralPath $stagedDll -PathType Leaf)) { throw 'Aucune nouvelle DLL produite.' }
# Read assembly metadata, without executing the plugin or faking SimHub assemblies.
$assembly = [System.Reflection.AssemblyName]::GetAssemblyName($stagedDll)
if ($assembly.Name -ne 'DIYAmbient.Plugin' -or $assembly.Version.ToString() -ne '0.3.0.0') { throw 'Identite/version de DLL inattendue.' }
Move-Item -LiteralPath $stagedDll -Destination $dll -Force
$sourceHashes = @($sources | ForEach-Object { @{ file=$_.FullName.Substring($root.Length + 1); sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash } })
@{ status='compiled-not-hardware-validated'; utc=[DateTime]::UtcNow.ToString('o'); version=$assembly.Version.ToString();
   sha256=(Get-FileHash $dll -Algorithm SHA256).Hash; compiler=(Get-Item $csc).VersionInfo.FileVersion;
   host_assemblies=$hostAssemblies; sources=$sourceHashes; hardware_tested=$false; simhub_loaded=$false } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'build-manifest.json') -Encoding UTF8
Write-Host "DLL compilee : $dll"
Write-Host 'Cela ne valide PAS son chargement dans SimHub, la capture Windows ni les LED.'
if ($Install) {
    if (Get-Process -Name 'SimHubWPF', 'SimHub' -ErrorAction SilentlyContinue) { throw 'Fermer SimHub avant installation.' }
    $destination = Join-Path $SimHubPath 'DIYAmbient.Plugin.dll'
    if (Test-Path -LiteralPath $destination) {
        Copy-Item -LiteralPath $destination -Destination ($destination + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
    }
    $pending = $destination + '.installing'
    try {
        Copy-Item -LiteralPath $dll -Destination $pending -Force
        if (Get-Process -Name 'SimHubWPF', 'SimHub' -ErrorAction SilentlyContinue) { throw 'SimHub vient de demarrer : installation annulee.' }
        Move-Item -LiteralPath $pending -Destination $destination -Force
    } finally { Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue }
    Write-Host 'Plugin copie. Relancer SimHub. Aucun firmware modifie.'
} else {
    Write-Host 'Aucune installation automatique. Copier uniquement cette DLL dans SimHub ferme, ou utiliser -Install.'
}
