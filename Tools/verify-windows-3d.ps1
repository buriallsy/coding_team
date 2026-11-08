#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GodotBin,
    [string]$LogDir,
    [switch]$Window
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Use this verifier on Windows.' }

$repoRoot = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$project = Join-Path $repoRoot 'BorrowingSun3D'
$GodotBin = (Resolve-Path -LiteralPath $GodotBin).Path
if (-not $LogDir) { $LogDir = Join-Path $repoRoot 'artifacts/verification/windows-3d' }
$logs = (New-Item -ItemType Directory -Path $LogDir -Force).FullName
$mode = if ($Window) { 'window' } else { 'headless' }
$metadata = Join-Path $logs "metadata-$mode.txt"

Push-Location $repoRoot
try {
    @(
        "utc=$([DateTimeOffset]::UtcNow.ToString('o'))"
        "os=$([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
        "architecture=$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)"
        "mode=$mode"
    ) | Set-Content -LiteralPath $metadata -Encoding utf8

    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the checkout SHA.' }
    "commit=$commit" | Add-Content -LiteralPath $metadata -Encoding utf8

    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the .NET SDK.' }
    "dotnet=$sdk" | Add-Content -LiteralPath $metadata -Encoding utf8
    if ($sdk -notmatch '^8\.0\.') { throw "Select a .NET 8 SDK before running this verifier. Found $sdk." }

    $engine = & $GodotBin --version
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the Godot engine.' }
    "godot=$engine" | Add-Content -LiteralPath $metadata -Encoding utf8
    if ($engine -notmatch '^4\.7\.1\.stable\.mono(?:\.|$)') {
        throw "Use the Godot 4.7.1 .NET Windows console executable. Found $engine."
    }

    & dotnet build (Join-Path $project 'BorrowingSun3D.csproj') --nologo 2>&1 |
        Tee-Object -FilePath (Join-Path $logs "build-$mode.log")
    if ($LASTEXITCODE -ne 0) { throw '3D C# build failed.' }

    & $GodotBin --headless --path $project --editor --import `
        --log-file (Join-Path $logs "import-$mode-engine.log") 2>&1 |
        Tee-Object -FilePath (Join-Path $logs "import-$mode.log")
    if ($LASTEXITCODE -ne 0) { throw '3D project import failed.' }

    $routeArgs = @('--path', $project, '--fixed-fps', '60', '--quit-after', '5000',
        '--log-file', (Join-Path $logs "route-$mode-engine.log"))
    if ($Window) {
        $captures = (New-Item -ItemType Directory -Path (Join-Path $logs 'captures') -Force).FullName
        $routeArgs += @('--resolution', '1280x720', '--', '--verify', "--capture-dir=$captures")
    }
    else {
        $routeArgs += @('--headless', '--', '--verify')
    }

    $routeLog = Join-Path $logs "route-$mode.log"
    & $GodotBin @routeArgs 2>&1 | Tee-Object -FilePath $routeLog
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $routeLog `
            -Pattern '^GRAYBOX_ROUTE PASSED assertions=[1-9][0-9]* failures=0$' -Quiet)) {
        throw 'The 3D route did not report success.'
    }
    if ($Window) {
        foreach ($file in @('intro.png', 'cargo-ready.png', 'cargo-delivered.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $captures $file) -PathType Leaf)) {
                throw "Missing window capture: $file"
            }
        }
    }
    Write-Output "3D WINDOWS VERIFICATION PASSED ($mode)"
}
finally {
    Pop-Location
}
