# Shared build helpers. Windows PowerShell 5.1; no download or installation here.
function Get-FrameworkDirectory {
    if (-not $env:WINDIR) { throw 'Ces scripts necessitent Windows et .NET Framework 4.8.' }
    foreach ($relative in @('Microsoft.NET\Framework64\v4.0.30319', 'Microsoft.NET\Framework\v4.0.30319')) {
        $candidate = Join-Path $env:WINDIR $relative
        if (Test-Path -LiteralPath (Join-Path $candidate 'csc.exe') -PathType Leaf) { return $candidate }
    }
    throw 'Compilateur .NET Framework introuvable. Aucun telechargement automatique ne sera effectue.'
}
function Get-ReferenceArguments([string]$Framework, [string[]]$Names) {
    foreach ($name in $Names) {
        $file = Join-Path $Framework $name
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { $file = Join-Path (Join-Path $Framework 'WPF') $name }
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Assembly systeme manquant : $name" }
        '/reference:"' + $file + '"'
    }
}
function Invoke-LoggedNative([string]$Executable, [string[]]$Arguments, [string]$LogPath) {
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "Executable introuvable : $Executable" }
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue' # Compiler stderr is diagnostic output, not a PowerShell exception.
    try {
        & $Executable @Arguments 2>&1 | Tee-Object -FilePath $LogPath | Out-Host
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw "Echec (code $code). Consulter : $LogPath" }
}
