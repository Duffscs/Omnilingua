param([ValidateSet('Debug','Release')][string] $Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$platformRoot = $PSScriptRoot
. (Join-Path $platformRoot 'build/Import-BuildEnvironment.ps1')
$msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1
if (!$msbuildPath) { throw 'MSBuild Visual Studio est requis.' }
function Invoke-Build([string] $Tool, [string[]] $Arguments) {
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Échec de $Tool ($LASTEXITCODE)." }
}
Push-Location $platformRoot
try {
    & ./tools/Build-Documentation.ps1
    foreach ($format in @('local_nuget','local_maven','local_pypi')) {
        New-Item -ItemType Directory -Path (Join-Path $env:PROJECT_DIR_PACKAGE $format) -Force | Out-Null
    }
    # Publier avant de restaurer les consommateurs de la version de développement mutable.
    foreach ($sdk in @('Omnilingua.Dotnet','Omnilingua.Dotnet.SourceGen','Omnilingua.Cpp')) {
        Invoke-Build dotnet @('build', "Omnilingua/$sdk/$sdk.csproj", '-c', $Configuration, '--nologo')
    }
    foreach ($language in @('Java','Python')) {
        Invoke-Build $msbuildPath @("Omnilingua/Omnilingua.$language/Bridge/Omnilingua.$language.Bridge.vcxproj", '/nologo', '/v:minimal', "/p:Configuration=$Configuration", '/p:Platform=x64')
    }
    Invoke-Build dotnet @('build','Omnilingua/Runtime/Omnilingua.csproj','-c',$Configuration,'--nologo')
    Invoke-Build dotnet @('restore','SamplePlugins/SdkPackages.csproj','--force','--no-cache')
    Invoke-Build dotnet @('build','SamplePlugins/Logger/Dotnet/Logger.Dotnet.csproj','-c',$Configuration,'--nologo')
    Invoke-Build dotnet @('build','SamplePlugins/Hello/Dotnet/Hello.Dotnet.csproj','-c',$Configuration,'--nologo')
    foreach ($sample in @('Java/Logger.Java.proj','Python/Logger.Python.proj')) {
        Invoke-Build $msbuildPath @("SamplePlugins/Logger/$sample",'/nologo','/v:minimal',"/p:Configuration=$Configuration",'/p:Platform=x64')
    }
    Invoke-Build $msbuildPath @('SamplePlugins/Hello/Java/Hello.Java.proj','/nologo','/v:minimal',"/p:Configuration=$Configuration")
    Invoke-Build $msbuildPath @('SamplePlugins/Hello/Python/Hello.Python.proj','/nologo','/v:minimal',"/p:Configuration=$Configuration")
    & ./tools/Build-Cpp.ps1 -Configuration $Configuration
    $runtimeOutput = Join-Path $platformRoot "Omnilingua/Runtime/bin/$Configuration/net10.0"
    Copy-Item "SamplePlugins/Logger/Dotnet/bin/$Configuration/net10.0/Logger.Dotnet.dll" $runtimeOutput -Force
    Copy-Item "SamplePlugins/Hello/Dotnet/bin/$Configuration/net10.0/Hello.Dotnet.dll" $runtimeOutput -Force
    Copy-Item "artifacts/cpp/$Configuration/plugins/Hello.Cpp.dll" $runtimeOutput -Force
    Copy-Item "artifacts/cpp/$Configuration/plugins/Logger.Cpp.dll" $runtimeOutput -Force
    Copy-Item "SamplePlugins/Hello/Java/bin/$Configuration/Hello.Java.jar" $runtimeOutput -Force
    Copy-Item "SamplePlugins/Logger/Java/bin/$Configuration/Logger.Java.jar" $runtimeOutput -Force
    Copy-Item "SamplePlugins/Logger/Python/bin/$Configuration/Logger.Python" $runtimeOutput -Recurse -Force
    Copy-Item "SamplePlugins/Hello/Python/bin/$Configuration/Hello.Python" $runtimeOutput -Recurse -Force
    Copy-Item -Path "SamplePlugins/config/*.json" -Destination $runtimeOutput -Force
    Copy-Item -LiteralPath "SamplePlugins/config/plugins.java.json" -Destination "$runtimeOutput/plugins.json" -Force
    New-Item -ItemType Directory -Path "$runtimeOutput/docs" -Force | Out-Null
    Remove-Item -LiteralPath "$runtimeOutput/docs/java-python.md","$runtimeOutput/docs/site/java-python.html" -ErrorAction SilentlyContinue
    Copy-Item -Path "docs/*.md" -Destination "$runtimeOutput/docs" -Force
    New-Item -ItemType Directory -Path "$runtimeOutput/docs/site" -Force | Out-Null
    Copy-Item -Path "docs/.vitepress/dist/*" -Destination "$runtimeOutput/docs/site" -Recurse -Force
    New-Item -ItemType Directory -Path artifacts -Force | Out-Null
    Compress-Archive -Path "$runtimeOutput/*" -DestinationPath "artifacts/Omnilingua.Runtime-9.9.9-win-x64-$Configuration.zip" -Force
} finally { Pop-Location }
