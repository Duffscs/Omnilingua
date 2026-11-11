param([ValidateSet('Debug','Release')][string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$platformRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $platformRoot 'build/Import-BuildEnvironment.ps1')
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
if ($cmakeCommand) { $cmakePath = $cmakeCommand.Source }
else {
    $visualStudioRoot = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
    $cmakePath = Join-Path $visualStudioRoot 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
}
if (!(Test-Path -LiteralPath $cmakePath)) { throw 'CMake 3.24 minimum est requis.' }
$packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$sdkRoot = Join-Path $packageCache 'omnilingua.cpp/9.9.9/build/native'
$cppBuildRoot = Join-Path $platformRoot "artifacts/cpp/$Configuration"
$configureArguments = @('-S', $platformRoot, '-B', $cppBuildRoot, "-DOmnilinguaCpp_DIR=$sdkRoot",
    "-DCMAKE_BUILD_TYPE=$Configuration", "-DOMNILINGUA_CPP_PYTHON_PATH=$platformRoot/.tmp/cpp-codegen")
if (!$visualStudioRoot) {
    $visualStudioRoot = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
}
$compatibleHeaders = Join-Path $visualStudioRoot 'VC/Tools/MSVC/14.44.35207/include'
if (Test-Path -LiteralPath $compatibleHeaders) {
    $ucrt = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/Include" -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'ucrt') } | Sort-Object Name -Descending | Select-Object -First 1
    $configureArguments += "-DOMNILINGUA_CPP_SYSTEM_INCLUDES=$compatibleHeaders;$($ucrt.FullName)/ucrt"
}
function Invoke-CMake([string[]] $Arguments) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $cmakePath @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $result = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($result -ne 0) { throw "Échec de CMake : $($Arguments -join ' ')" }
}
Invoke-CMake $configureArguments
Invoke-CMake @('--build', $cppBuildRoot, '--config', $Configuration, '--parallel')
Invoke-CMake @('--install', $cppBuildRoot, '--config', $Configuration, '--prefix', "$cppBuildRoot/plugins")
