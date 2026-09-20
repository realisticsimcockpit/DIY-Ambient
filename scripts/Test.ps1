[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$exe = Join-Path $out 'DIYAmbient.CoreTests.exe'
# Never allow an old test executable to masquerade as a new successful build.
Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $out 'test-result.json') -Force -ErrorAction SilentlyContinue
$framework = Get-FrameworkDirectory
$csc = Join-Path $framework 'csc.exe'
$lines = @('/nologo', '/target:exe', '/platform:anycpu', '/langversion:5', '/warn:4', '/optimize+', '/codepage:65001', "/out:`"$exe`"")
$lines += Get-ReferenceArguments $framework @('System.dll', 'System.Core.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll')
$lines += Get-ChildItem (Join-Path $root 'src\DIYAmbient.Core') -Filter '*.cs' | Sort-Object Name | ForEach-Object { '"' + $_.FullName + '"' }
$lines += '"' + (Join-Path $root 'tests\CoreTests.cs') + '"'
$rsp = Join-Path $out 'tests.rsp'
$lines | Set-Content -LiteralPath $rsp -Encoding UTF8
Invoke-LoggedNative -Executable $csc -Arguments @("@$rsp") -LogPath (Join-Path $out 'compile.log')
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Le compilateur ne fournit pas de nouvel executable de test.' }
Invoke-LoggedNative -Executable $exe -Arguments @() -LogPath (Join-Path $out 'run.log')
@{ status='passed'; utc=[DateTime]::UtcNow.ToString('o'); runner='Windows .NET Framework';
   compiler=(Get-Item $csc).VersionInfo.FileVersion; hardware_exercised=$false; simhub_exercised=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'test-result.json') -Encoding UTF8
