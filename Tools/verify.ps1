param(
    [string]$GodotBin,
    [switch]$Offline
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if ($GodotBin) { $GodotBin = (Resolve-Path -LiteralPath $GodotBin).Path }
$gameProject = Join-Path $repoRoot 'BorrowingSun2D'
$domainProject = Join-Path $repoRoot 'Tests/DomainChecks/DomainChecks.csproj'
$logs = Join-Path $repoRoot 'artifacts/verification'
$evidence = Join-Path $gameProject 'Docs/Evidence/round-03'
$qaDir = $null
Push-Location $repoRoot
$savedEnvironment = @{}
$envKeys = @('APPDATA','DOTNET_CLI_HOME','NUGET_PACKAGES','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_CLI_UI_LANGUAGE')
foreach ($key in $envKeys) { $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
try {
    New-Item -ItemType Directory -Path $logs -Force | Out-Null
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
    & dotnet build $domainProject -p:UseAppHost=false @buildArgs 2>&1 | Tee-Object (Join-Path $logs 'domain-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Domain build failed.' }
    $domainLog = Join-Path $logs 'domain-checks.log'
    $domainAssembly = Join-Path $repoRoot 'Tests/DomainChecks/bin/Debug/net8.0/DomainChecks.dll'
    & dotnet $domainAssembly 2>&1 | Tee-Object $domainLog
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $domainLog -Pattern '^DOMAIN_CHECKS PASSED:' -Quiet)) {
        throw 'Domain checks did not report success.'
    }

    & dotnet build (Join-Path $gameProject 'BorrowingSun2D.csproj') @buildArgs 2>&1 | Tee-Object (Join-Path $logs 'game-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    if (-not $GodotBin) {
        Write-Output 'VERIFICATION PASSED: domain checks and Godot C# build. Pass -GodotBin to run both scene routes.'
        return
    }

    $routeLog = Join-Path $logs 'three-shore-route.log'
    & $GodotBin --headless --path $gameProject --fixed-fps 60 --quit-after 14000 `
        --log-file (Join-Path $logs 'three-shore-engine.log') -- --verify 2>&1 | Tee-Object $routeLog
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $routeLog -Pattern '^TOWER_ROUTE PASSED$' -Quiet)) {
        throw 'Three-shore route did not report success.'
    }

    $qaDir = Join-Path ([System.IO.Path]::GetTempPath()) ('borrowingsun-round03-' + [Guid]::NewGuid().ToString('N'))
    $qaProject = Join-Path $qaDir 'project'
    $qaHarness = Join-Path $qaProject 'VerificationHarness'
    New-Item -ItemType Directory -Path $qaHarness -Force | Out-Null
    foreach ($item in @('BorrowingSun2D.csproj', 'project.godot', 'Scenes', 'Scripts')) {
        Copy-Item -LiteralPath (Join-Path $gameProject $item) -Destination $qaProject -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $evidence 'round03-cargo-probe.cs.txt') `
        -Destination (Join-Path $qaHarness 'Round03CargoProbe.cs')
    $qaLauncher = Join-Path $qaDir 'round03-cargo-launcher.gd'
    Copy-Item -LiteralPath (Join-Path $evidence 'round03-cargo-launcher.gd.txt') -Destination $qaLauncher

    & dotnet build (Join-Path $qaProject 'BorrowingSun2D.csproj') @buildArgs 2>&1 | Tee-Object (Join-Path $logs 'round03-probe-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Round 03 probe build failed.' }
    $cargoLog = Join-Path $logs 'round03-cargo.log'
    & $GodotBin --headless --path $qaProject --fixed-fps 60 --quit-after 14000 `
        --log-file (Join-Path $logs 'round03-cargo-engine.log') --script $qaLauncher 2>&1 | Tee-Object $cargoLog
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $cargoLog -Pattern '^ROUND_03_CARGO_QA PASSED' -Quiet)) {
        throw 'Round 03 cargo route did not report success.'
    }
    Write-Output 'VERIFICATION PASSED: domain, three-shore route, and Round 03 cargo route.'
}
finally {
    if ($qaDir -and (Test-Path -LiteralPath $qaDir)) { Remove-Item -LiteralPath $qaDir -Recurse -Force }
    foreach ($key in $envKeys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process') }
    Pop-Location
}
