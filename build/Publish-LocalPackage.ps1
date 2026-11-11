param(
    [Parameter(Mandatory)][ValidateSet('Nuget', 'Maven')][string] $Kind,
    [Parameter(Mandatory)][string] $PackageId,
    [string] $Version = '9.9.9',
    [string] $Artifact,
    [string] $Feed,
    [switch] $Restore
)
$ErrorActionPreference = 'Stop'
if ($Version -ne '9.9.9' -or $PackageId -notmatch '^[a-zA-Z0-9_]+([.-][a-zA-Z0-9_]+)*$') { throw 'Seuls les packages de développement 9.9.9 sont autorisés.' }
if ($Kind -eq 'Nuget') {
    $cacheRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $cacheRoot = [IO.Path]::GetFullPath($cacheRoot).TrimEnd('\', '/')
    $cacheVersion = [IO.Path]::GetFullPath((Join-Path $cacheRoot "$($PackageId.ToLowerInvariant())/$Version"))
    $expectedPath = Join-Path (Join-Path $cacheRoot $PackageId.ToLowerInvariant()) '9.9.9'
    if ($cacheVersion -ne [IO.Path]::GetFullPath($expectedPath) -or !$cacheVersion.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Chemin de cache NuGet invalide.' }
    if (Test-Path -LiteralPath $cacheVersion) { Remove-Item -LiteralPath $cacheVersion -Recurse -Force }
    Write-Host "Cache invalidé : $PackageId $Version"
    return
}
$relative = "io/omnilingua/$PackageId/$Version"
$repository = [IO.Path]::GetFullPath($Feed)
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE '.m2/repository'))
$cacheVersion = [IO.Path]::GetFullPath((Join-Path $cacheRoot $relative))
if (!$cacheVersion.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $cacheVersion -Leaf) -ne '9.9.9') { throw 'Chemin de cache Maven invalide.' }
$published = Join-Path $repository $relative
if ($Restore) {
    New-Item -ItemType Directory -Path $cacheVersion -Force | Out-Null
    foreach ($extension in @('jar', 'pom')) {
        $file = "$PackageId-$Version.$extension"
        Copy-Item -LiteralPath (Join-Path $published $file) -Destination (Join-Path $cacheVersion $file) -Force
    }
} else {
    New-Item -ItemType Directory -Path $published -Force | Out-Null
    Copy-Item -LiteralPath $Artifact -Destination (Join-Path $published "$PackageId-$Version.jar") -Force
    $pom = '<project xmlns="http://maven.apache.org/POM/4.0.0"><modelVersion>4.0.0</modelVersion><groupId>io.omnilingua</groupId><artifactId>' + $PackageId + '</artifactId><version>' + $Version + '</version><name>Omnilingua Java SDK</name></project>'
    [IO.File]::WriteAllText((Join-Path $published "$PackageId-$Version.pom"), $pom)
    if (Test-Path -LiteralPath $cacheVersion) { Remove-Item -LiteralPath $cacheVersion -Recurse -Force }
}
