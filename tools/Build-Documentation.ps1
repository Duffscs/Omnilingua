param()
$ErrorActionPreference = 'Stop'
$platformRoot = Split-Path $PSScriptRoot -Parent
$documentationRoot = Join-Path $platformRoot 'docs'
function Invoke-Npm([string[]] $Arguments) {
    $previousPreference = $ErrorActionPreference
    try {
        # VitePress écrit sa progression sur stderr, même lorsque le build réussit.
        $ErrorActionPreference = 'Continue'
        & npm @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $result = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($result -ne 0) { throw "Échec de npm $($Arguments -join ' ') ($result)." }
}
Push-Location $documentationRoot
try {
    Invoke-Npm @('ci')
    Invoke-Npm @('run', 'build')
    $artifactsRoot = Join-Path $platformRoot 'artifacts'
    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
    Compress-Archive -Path (Join-Path $documentationRoot '.vitepress/dist/*') `
        -DestinationPath (Join-Path $artifactsRoot 'Omnilingua.Documentation-9.9.9.zip') -Force
} finally { Pop-Location }
