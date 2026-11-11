$platformEnvironmentRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$platformEnvironmentFile = Join-Path $platformEnvironmentRoot '.env'
if ([string]::IsNullOrWhiteSpace($env:PROJECT_DIR_PACKAGE) -and (Test-Path -LiteralPath $platformEnvironmentFile)) {
    foreach ($line in Get-Content -LiteralPath $platformEnvironmentFile -Encoding UTF8) {
        if ($line -match '^\s*PROJECT_DIR_PACKAGE\s*=\s*(.+?)\s*$') {
            $env:PROJECT_DIR_PACKAGE = $Matches[1].Trim()
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($env:PROJECT_DIR_PACKAGE)) {
    $env:PROJECT_DIR_PACKAGE = Join-Path $platformEnvironmentRoot 'artifacts/packages'
}
if (![IO.Path]::IsPathRooted($env:PROJECT_DIR_PACKAGE)) {
    $env:PROJECT_DIR_PACKAGE = Join-Path $platformEnvironmentRoot $env:PROJECT_DIR_PACKAGE
}
$env:PROJECT_DIR_PACKAGE = [IO.Path]::GetFullPath($env:PROJECT_DIR_PACKAGE).Replace('\', '/')
