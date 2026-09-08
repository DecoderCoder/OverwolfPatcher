param([switch]$Test, [switch]$SkipProfiler)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $repo 'artifacts\dotnet'
$env:NUGET_PACKAGES = Join-Path $repo 'artifacts\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $repo 'OverwolfPatcher\OverwolfPatcher.csproj'
if ($Test) { $project = Join-Path $repo 'tests\OverwolfPatcher.Tests.csproj' }
dotnet restore $project --configfile (Join-Path $PSScriptRoot 'NuGet.Config') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $project --configuration Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not $SkipProfiler) {
    $vsRoot = Get-ChildItem 'C:\Program Files (x86)\Microsoft Visual Studio' -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'VC\Auxiliary\Build\vcvars64.bat') } |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $vsRoot) { throw 'Visual C++ x64 build tools were not found. Install them or use -SkipProfiler.' }
    $nativeOutput = Join-Path $repo 'OverwolfPatcher\bin\Release\net48'
    $nativeIntermediate = Join-Path $repo 'build\native\Release'
    New-Item -ItemType Directory -Force -Path $nativeOutput, $nativeIntermediate | Out-Null
    $vcvars = Join-Path $vsRoot 'VC\Auxiliary\Build\vcvars64.bat'
    $source = Join-Path $repo 'native\Profiler.cpp'
    $include = Join-Path $repo 'native'
    $dll = Join-Path $nativeOutput 'OverwolfPatcher.Profiler.x64.dll'
    $object = Join-Path $nativeIntermediate 'Profiler.obj'
    $definition = Join-Path $repo 'native\OverwolfPatcher.Profiler.def'
    $importLibrary = Join-Path $nativeIntermediate 'OverwolfPatcher.Profiler.x64.lib'
    $pdb = Join-Path $nativeIntermediate 'OverwolfPatcher.Profiler.x64.pdb'
    $command = 'call "' + $vcvars + '" && cl.exe /nologo /LD /EHsc /std:c++17 /DWIN32 /D_WINDOWS /D_USRDLL /DOVERWOLFPATCHER_PROFILER_EXPORTS /I"' + $include + '" /Fo"' + $object + '" "' + $source + '" /link /DEF:"' + $definition + '" /OUT:"' + $dll + '" /IMPLIB:"' + $importLibrary + '" /PDB:"' + $pdb + '" bcrypt.lib ole32.lib'
    & cmd.exe /d /c $command
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if ($Test) {
    $harnessProject = Join-Path $repo 'tests\ProfilerHarness\ProfilerHarness.csproj'
    dotnet restore $harnessProject --configfile (Join-Path $PSScriptRoot 'NuGet.Config') --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build $harnessProject --configuration Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & (Join-Path $repo 'tests\bin\Release\net48\OverwolfPatcher.Tests.exe')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $harness = Join-Path $repo 'tests\ProfilerHarness\bin\Release\net48\OverwolfPatcher.ProfilerHarness.exe'
    $profiler = Join-Path $repo 'OverwolfPatcher\bin\Release\net48\OverwolfPatcher.Profiler.x64.dll'
    $fixtureLog = Join-Path $repo 'artifacts\profiler\fixture-build.log'
    New-Item -ItemType Directory -Force -Path (Split-Path $fixtureLog) | Out-Null
    Remove-Item -LiteralPath $fixtureLog -ErrorAction SilentlyContinue
    $env:COR_ENABLE_PROFILING = '1'
    $env:COR_PROFILER = '{4D7C38E9-7C8A-4F5D-9D2C-1D3E7BC9F1A4}'
    $env:COR_PROFILER_PATH = $profiler
    $env:COR_PROFILER_PATH_64 = $profiler
    $env:COMPLUS_ProfAPI_ProfilerCompatibilitySetting = 'EnableV2Profiler'
    $env:OVERWOLF_PATCHER_PROFILER_TEST = '1'
    $env:OVERWOLF_PATCHER_PROFILER_MODE = 'premium'
    $env:OVERWOLF_PATCHER_PROFILER_LOG = $fixtureLog
    $env:OVERWOLF_PATCHER_PROFILER_LOG_PER_PROCESS = '0'
    $env:OVERWOLF_PATCHER_PROFILER_VERBOSE = '1'
    $env:OVERWOLF_PATCHER_APP = 'cghphpbjeabdkomiphingnegihoigeggcfphdofo'
    $env:OVERWOLF_PATCHER_PLANS = '61'
    & $harness
    $harnessExit = $LASTEXITCODE
    foreach ($name in @('COR_ENABLE_PROFILING', 'COR_PROFILER', 'COR_PROFILER_PATH',
            'COR_PROFILER_PATH_64', 'COMPLUS_ProfAPI_ProfilerCompatibilitySetting',
            'OVERWOLF_PATCHER_PROFILER_TEST', 'OVERWOLF_PATCHER_PROFILER_MODE',
            'OVERWOLF_PATCHER_PROFILER_LOG', 'OVERWOLF_PATCHER_PROFILER_LOG_PER_PROCESS',
            'OVERWOLF_PATCHER_PROFILER_VERBOSE', 'OVERWOLF_PATCHER_APP', 'OVERWOLF_PATCHER_PLANS')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    if ($harnessExit -ne 0) { exit $harnessExit }
    Write-Host "Native profiler fixture passed. Log: $fixtureLog"
    exit 0
}
