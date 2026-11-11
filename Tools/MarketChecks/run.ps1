# Runs the production C# domain checks without NuGet downloads or Unity.
$ErrorActionPreference = 'Stop'
$marketExitCode = 1
$marketEnvironmentNames = @(
    'APPDATA', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'DOTNET_NOLOGO',
    'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH',
    'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'DOTNET_CLI_TELEMETRY_OPTOUT'
)
$marketPreviousEnvironment = @{}
foreach ($marketName in $marketEnvironmentNames) {
    $marketPreviousEnvironment[$marketName] = [Environment]::GetEnvironmentVariable($marketName, 'Process')
}

try {
    $marketDotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $marketDotnetRoot = Split-Path -Parent $marketDotnet
    $marketReferenceRoot = Join-Path $marketDotnetRoot 'packs\Microsoft.NETCore.App.Ref'
    $marketReference = Get-ChildItem -LiteralPath $marketReferenceRoot -Directory |
        Where-Object { $_.Name -match '^8\.0\.\d+$' } |
        Sort-Object { [Version]$_.Name } -Descending |
        Select-Object -First 1
    if ($null -eq $marketReference) {
        throw 'An installed .NET 8 reference pack is required. Install a .NET 8 SDK, then run this script again.'
    }

    $marketTemporary = Join-Path ([IO.Path]::GetTempPath()) 'BorrowingSun-MarketChecks'
    [IO.Directory]::CreateDirectory($marketTemporary) | Out-Null
    $marketConfig = Join-Path $marketTemporary 'NuGet.Config'
    $marketTargets = Join-Path $marketTemporary 'installed-reference-pack.targets'
    [IO.File]::WriteAllText($marketConfig, '<configuration><packageSources><clear /></packageSources></configuration>')
    $marketVersion = $marketReference.Name
    $marketTargetXml = '<Project><PropertyGroup><EnableTargetingPackDownload>false</EnableTargetingPackDownload><EnableRuntimePackDownload>false</EnableRuntimePackDownload></PropertyGroup><ItemGroup><KnownFrameworkReference Update="Microsoft.NETCore.App" TargetingPackVersion="' + $marketVersion + '" LatestRuntimeFrameworkVersion="' + $marketVersion + '" /></ItemGroup></Project>'
    [IO.File]::WriteAllText($marketTargets, $marketTargetXml)

    # Redirect only this process, and restore even if restore/build/tests fail.
    $env:APPDATA = $marketTemporary
    $env:DOTNET_CLI_HOME = $marketTemporary
    $env:NUGET_PACKAGES = Join-Path $marketTemporary 'packages'
    $env:DOTNET_NOLOGO = 'true'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

    $marketProject = Join-Path $PSScriptRoot 'MarketChecks.csproj'
    $marketTargetArgument = '-p:CustomAfterMicrosoftCommonTargets=' + $marketTargets
    Write-Output ('Using installed .NET 8 reference pack ' + $marketVersion + '; C# language version 9.')
    & $marketDotnet restore $marketProject --configfile $marketConfig $marketTargetArgument --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw ('Offline restore failed with exit code ' + $LASTEXITCODE + '.') }
    & $marketDotnet run --project $marketProject --no-restore $marketTargetArgument
    $marketExitCode = $LASTEXITCODE
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
}
finally {
    foreach ($marketName in $marketEnvironmentNames) {
        if ($null -eq $marketPreviousEnvironment[$marketName]) {
            Remove-Item -LiteralPath ('Env:' + $marketName) -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($marketName, $marketPreviousEnvironment[$marketName], 'Process')
        }
    }
}

exit $marketExitCode
