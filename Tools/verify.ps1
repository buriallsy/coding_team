param(
    [string]$GodotBin,
    [switch]$Offline
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
$savedEnvironment = @{}
$envKeys = @('APPDATA','DOTNET_CLI_HOME','NUGET_PACKAGES','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_CLI_UI_LANGUAGE')
foreach ($key in $envKeys) { $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
try {
    New-Item -ItemType Directory -Path 'artifacts/verification' -Force | Out-Null
    $buildArgs = @('--nologo')
    if ($Offline) {
        if (-not $GodotBin -or -not (Test-Path -LiteralPath $GodotBin)) { throw 'Offline mode needs -GodotBin.' }
        $localCache = Join-Path $repoRoot '.local'
        $localConfig = Join-Path $localCache 'appdata/NuGet/NuGet.Config'
        New-Item -ItemType Directory -Path (Split-Path $localConfig) -Force | Out-Null
        $packages = Join-Path (Split-Path $GodotBin) 'GodotSharp/Tools/nupkgs'
        if (-not (Test-Path -LiteralPath $packages)) { throw 'Godot .NET package directory missing.' }
        $escapedSource = [Security.SecurityElement]::Escape($packages)
        Set-Content -LiteralPath $localConfig -Encoding utf8 -Value ('<configuration><packageSources><clear/><add key="godot-local" value="' + $escapedSource + '" /></packageSources></configuration>')
        $dotnetRoot = Split-Path (Get-Command dotnet).Source
        $refPack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory |
            Where-Object Name -Like '8.0.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
        if (-not $refPack) { throw 'An installed .NET 8 reference pack is required.' }
        $targets = Join-Path $localCache 'offline.targets'
        $version = $refPack.Name
        Set-Content -LiteralPath $targets -Encoding utf8 -Value ('<Project><ItemGroup><KnownFrameworkReference Update="Microsoft.NETCore.App;Microsoft.WindowsDesktop.App;Microsoft.WindowsDesktop.App.WPF;Microsoft.WindowsDesktop.App.WindowsForms;Microsoft.AspNetCore.App" TargetingPackVersion="' + $version + '" LatestRuntimeFrameworkVersion="' + $version + '" /></ItemGroup></Project>')
        $env:APPDATA = Join-Path $localCache 'appdata'
        $env:DOTNET_CLI_HOME = $localCache
        $env:NUGET_PACKAGES = Join-Path $localCache 'packages'
        $buildArgs += "-p:CustomAfterMicrosoftCommonTargets=$targets"
    }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
    & dotnet build Tests/DomainChecks -p:UseAppHost=false @buildArgs 2>&1 | Tee-Object artifacts/verification/domain-build.log
    if ($LASTEXITCODE -ne 0) { throw 'Domain build failed.' }
    & dotnet Tests/DomainChecks/bin/Debug/net8.0/DomainChecks.dll 2>&1 | Tee-Object artifacts/verification/domain-checks.log
    if ($LASTEXITCODE -ne 0) { throw 'Domain checks failed.' }
    & dotnet build BorrowingSun2D/BorrowingSun2D.csproj @buildArgs 2>&1 | Tee-Object artifacts/verification/game-build.log
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    if ($GodotBin) {
        & $GodotBin --headless --path "$repoRoot/BorrowingSun2D" --fixed-fps 60 -- --verify 2>&1 | Tee-Object artifacts/verification/mvp-route.log
        if ($LASTEXITCODE -ne 0) { throw 'MVP route failed.' }
        $qaScript = Join-Path $repoRoot 'artifacts/verification/independent-route.gd'
        Copy-Item -LiteralPath 'BorrowingSun2D/Docs/Evidence/independent-route.gd.txt' -Destination $qaScript -Force
        & $GodotBin --headless --path "$repoRoot/BorrowingSun2D" --fixed-fps 60 --script $qaScript 2>&1 | Tee-Object artifacts/verification/independent-route.log
        if ($LASTEXITCODE -ne 0) { throw 'Independent MVP route failed.' }
    }
    Write-Output 'VERIFICATION PASSED for executed checks; see logs. Full-level integration and macOS are not implied.'
}
finally {
    foreach ($key in $envKeys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process') }
    Pop-Location
}
