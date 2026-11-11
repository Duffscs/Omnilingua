param([ValidateSet('Debug','Release')][string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $projectRoot 'build/Import-BuildEnvironment.ps1')
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1
function Invoke-Test([string] $Tool, [string[]] $Arguments) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $Tool @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $commandResult = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($commandResult -ne 0) { throw "Échec du contrôle : $Tool $($Arguments -join ' ')" }
}
$previousConfiguration = $env:OMNILINGUA_TEST_CONFIGURATION
$env:OMNILINGUA_TEST_CONFIGURATION = $Configuration
Push-Location $projectRoot
try {
    & ./tools/Build-Cpp.ps1 -Configuration $Configuration
    foreach ($test in @('Dotnet.SourceGen.Tests','Logger.SmokeTests')) {
        Invoke-Test dotnet @('run','--project',"tests/$test",'-c',$Configuration,'-p:PublishLocalPackages=false')
    }
    $cmake = Get-Command cmake -ErrorAction SilentlyContinue
    $cmakeBinary = if ($cmake) { $cmake.Source } else { Join-Path (Split-Path $msbuild -Parent) '../../../Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe' }
    $ctest = Join-Path (Split-Path $cmakeBinary -Parent) 'ctest.exe'
    Invoke-Test $ctest @('--test-dir', "artifacts/cpp/$Configuration", '-C', $Configuration, '--output-on-failure')
    Invoke-Test python @('-I','-B','tests/Python.SdkTests/test_sdk.py','-v')
    Invoke-Test python @('-I','-B','tests/Packaging.Tests/test_packages.py','-v')
} finally { Pop-Location; $env:OMNILINGUA_TEST_CONFIGURATION = $previousConfiguration }
