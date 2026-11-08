#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GodotBin,
    [Parameter(Mandatory)][string]$BaselineRoot,
    [Parameter(Mandatory)][string]$LogDir,
    [Parameter(Mandatory)][ValidateSet('2D', '3D')][string]$Version,
    [string]$RenderingDriver
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Use this verifier on a Windows desktop.' }
$Version = $Version.ToUpperInvariant()

function Replace-ProbeText([string]$Text, [string]$Before, [string]$After) {
    $offset = $Text.IndexOf($Before, [StringComparison]::Ordinal)
    if ($offset -lt 0 -or $Text.IndexOf($Before, $offset + $Before.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "Expected one occurrence in the QA probe: $Before"
    }
    return $Text.Replace($Before, $After)
}

$repoRoot = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$baselineRoot = (Resolve-Path -LiteralPath $BaselineRoot).Path
$GodotBin = (Resolve-Path -LiteralPath $GodotBin).Path
$logs = (New-Item -ItemType Directory -Path (Join-Path $LogDir ($Version.ToLowerInvariant())) -Force).FullName
$captures = (New-Item -ItemType Directory -Path (Join-Path $logs 'captures') -Force).FullName
$qaRoot = Join-Path $repoRoot "artifacts/desktop-qa-$([Guid]::NewGuid().ToString('N'))"
$qaCaptures = Join-Path $qaRoot 'captures'
$projectName = "BorrowingSun$Version"
$sourceRoot = if ($Version -eq '2D') { $baselineRoot } else { $repoRoot }
$sourceProject = Join-Path $sourceRoot $projectName
$project = Join-Path $qaRoot $projectName
$captureNames = @('intro.png', 'cargo-ready.png', 'cargo-delivered-first.png', 'west-after-r.png', 'cargo-delivered.png')
$displayTrace = 'GD.Print($"WINDOWS_DESKTOP display={DisplayServer.GetName()} driver={RenderingServer.GetCurrentRenderingDriverName()} method={RenderingServer.GetCurrentRenderingMethod()}");'

New-Item -ItemType Directory -Path $project, $qaCaptures -Force | Out-Null
Push-Location $qaRoot
try {
    $metadata = Join-Path $logs 'metadata.txt'
    @(
        "utc=$([DateTimeOffset]::UtcNow.ToString('o'))"
        "os=$([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
        "architecture=$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)"
        "version=$Version"
        "requested_rendering_driver=$RenderingDriver"
    ) | Set-Content -LiteralPath $metadata -Encoding utf8
    $commit = & git -C $sourceRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the source checkout.' }
    "source_checkout_sha=$commit" | Add-Content -LiteralPath $metadata -Encoding utf8

    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^8\.0\.') { throw "Select a .NET 8 SDK. Found $sdk." }
    "dotnet=$sdk" | Add-Content -LiteralPath $metadata -Encoding utf8
    $engine = & $GodotBin --version
    if ($LASTEXITCODE -ne 0 -or $engine -notmatch '^4\.7\.1\.stable\.mono(?:\.|$)') {
        throw "Use Godot 4.7.1 .NET for Windows. Found $engine."
    }
    "godot=$engine" | Add-Content -LiteralPath $metadata -Encoding utf8

    foreach ($item in @("$projectName.csproj", 'project.godot', 'Scenes', 'Scripts')) {
        Copy-Item -LiteralPath (Join-Path $sourceProject $item) -Destination $project -Recurse
    }

    if ($Version -eq '2D') {
        $archive = Join-Path $sourceProject 'Docs/Evidence/round-04'
        $harness = (New-Item -ItemType Directory -Path (Join-Path $project 'VerificationHarness')).FullName
        $launcher = Join-Path $qaRoot 'round04-acceptance-launcher.gd'
        Copy-Item -LiteralPath (Join-Path $archive 'round04-acceptance-launcher.gd.txt') -Destination $launcher

        $probe = (Get-Content -LiteralPath (Join-Path $archive 'round04-acceptance-probe.cs.txt') -Raw).Replace("`r`n", "`n")
        $probe = Replace-ProbeText $probe 'OS.GetCmdlineUserArgs().Length == 3' 'OS.GetCmdlineUserArgs().Length == 5'
        $probe = Replace-ProbeText $probe 'if (captures.Length != 3) return;' 'if (captures.Length != 5) return;'
        $probe = Replace-ProbeText $probe `
            'await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);' `
            'RenderingServer.ForceDraw(false);'
        $before = '                GD.Print($"ROUND_04_ACCEPTANCE_CAPTURE PASSED assertions={assertions} failures=0");'
        $after = @"
                await ResetFromDelivery("after the first normal delivery");
                await Capture(3, "R reset to the live west low bridge");
                await NormalDelivery(capture: false);
                await Capture(4, "replay delivery 1/1");
$before
"@
        $probe = Replace-ProbeText $probe $before $after
        $before = '                GetWindow().FocusEntered += () => GD.Print("TRACE native viewport focused");'
        $probe = Replace-ProbeText $probe $before "                $displayTrace`n$before"
        Set-Content -LiteralPath (Join-Path $harness 'Round04AcceptanceProbe.cs') -Value $probe -Encoding utf8 -NoNewline

        $captureArgs = foreach ($file in $captureNames) { Join-Path $qaCaptures $file }
        $userArgs = @('--script', $launcher, '--') + $captureArgs
        $passPattern = '^ROUND_04_ACCEPTANCE_CAPTURE PASSED assertions=[1-9][0-9]* failures=0$'
        $deliveryPattern = '^PASS actual G at the terminal confirms exactly one crate \(1/1\) \|'
        $resetPattern = '^PASS after the first normal delivery: player and usable low bridge return to the entrance \|'
    }
    else {
        $shared = (New-Item -ItemType Directory -Path (Join-Path $qaRoot 'BorrowingSun2D')).FullName
        Copy-Item -LiteralPath (Join-Path $repoRoot 'BorrowingSun2D/Scripts') -Destination $shared -Recurse
        $probePath = Join-Path $project 'Scripts/Verification/GrayboxRouteCheck.cs'
        $probe = (Get-Content -LiteralPath $probePath -Raw).Replace("`r`n", "`n")
        $before = @'
            await LaunchAndRide(blockAfterLaunch: true);
            await ConfirmDelivery();

            await ResetToEntrance(Key.R, "R from the finish overlay");
'@
        $after = @'
            await LaunchAndRide(blockAfterLaunch: true);
            await ConfirmDelivery();
            await Capture("cargo-delivered-first.png");

            await ResetToEntrance(Key.R, "R from the finish overlay");
            await Capture("west-after-r.png");
'@
        $probe = Replace-ProbeText $probe ($before.Replace("`r`n", "`n")) ($after.Replace("`r`n", "`n"))
        $before = '                Directory.CreateDirectory(captureDirectory);'
        $probe = Replace-ProbeText $probe $before "$before`n                $displayTrace"
        Set-Content -LiteralPath $probePath -Value $probe -Encoding utf8 -NoNewline

        $userArgs = @('--', '--verify', "--capture-dir=$qaCaptures")
        $passPattern = '^GRAYBOX_ROUTE PASSED assertions=[1-9][0-9]* failures=0$'
        $deliveryPattern = '^PASS actual G at the delivery marker confirms 1/1 and removes the crate from the car \|'
        $resetPattern = '^PASS R from the finish overlay: expected dock=Entry, bridge flag=True, native bridge hit=True \|'
    }

    & dotnet build (Join-Path $project "$projectName.csproj") --nologo 2>&1 |
        Tee-Object -FilePath (Join-Path $logs 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "$Version QA build failed." }

    & $GodotBin --headless --path $project --editor --import `
        --log-file (Join-Path $logs 'import-engine.log') 2>&1 |
        Tee-Object -FilePath (Join-Path $logs 'import.log')
    if ($LASTEXITCODE -ne 0) { throw "$Version QA project import failed." }

    $routeArgs = @('--path', $project, '--display-driver', 'windows', '--resolution', '1280x720', '--fixed-fps', '60',
        '--quit-after', '5000', '--audio-driver', 'Dummy', '--log-file', (Join-Path $logs 'route-engine.log'))
    if ($RenderingDriver) { $routeArgs += @('--rendering-driver', $RenderingDriver) }
    $routeArgs += $userArgs
    $routeLog = Join-Path $logs 'route.log'
    & $GodotBin @routeArgs 2>&1 | Tee-Object -FilePath $routeLog
    $routeExit = $LASTEXITCODE
    foreach ($frame in Get-ChildItem -LiteralPath $qaCaptures -File -Filter '*.png') {
        Copy-Item -LiteralPath $frame.FullName -Destination $captures
    }
    if ($routeExit -ne 0 -or -not (Select-String -LiteralPath $routeLog -Pattern $passPattern -Quiet)) {
        throw "$Version windowed route did not report success."
    }

    $driverPattern = if ($RenderingDriver) { [Regex]::Escape($RenderingDriver) } else { '\S+' }
    $displayPattern = "^WINDOWS_DESKTOP display=Windows driver=$driverPattern method=gl_compatibility$"
    if (-not (Select-String -LiteralPath $routeLog -Pattern $displayPattern -Quiet)) {
        throw "$Version route did not report the expected Windows display and renderer."
    }
    if (@(Select-String -LiteralPath $routeLog -Pattern $deliveryPattern).Count -ne 2 -or
        -not (Select-String -LiteralPath $routeLog -Pattern $resetPattern -Quiet)) {
        throw "$Version route did not confirm both deliveries and the reset low bridge."
    }

    foreach ($file in $captureNames) {
        $path = Join-Path $qaCaptures $file
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing window capture: $file" }
        $png = [IO.File]::ReadAllBytes($path)
        # The PNG signature and big-endian IHDR fields identify the actual viewport size.
        if ($png.Length -lt 24 -or [Convert]::ToHexString($png, 0, 8) -ne '89504E470D0A1A0A' -or
            [Convert]::ToHexString($png, 12, 4) -ne '49484452') {
            throw "Expected a valid PNG window capture: $file"
        }
        $width = [Convert]::ToUInt32([Convert]::ToHexString($png, 16, 4), 16)
        $height = [Convert]::ToUInt32([Convert]::ToHexString($png, 20, 4), 16)
        # The desktop rounds the viewport to whole physical pixels.
        $heightError = [Math]::Abs($width * 9.0 / 16 - $height)
        if ($width -lt 960 -or $height -lt 540 -or $heightError -gt 1) {
            throw "Expected a 16:9 viewport within one pixel and at least 960x540. ${file}: ${width}x${height}"
        }
        "capture=$file ${width}x${height}" | Add-Content -LiteralPath $metadata -Encoding utf8
    }
    Write-Output "WINDOWS DESKTOP VERIFICATION PASSED ($Version)"
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $qaRoot -Recurse -Force
}
