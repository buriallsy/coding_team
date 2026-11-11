[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityManagedPath = 'E:\Unity\2022.3.39f1\Editor\Data\Managed',
    [string]$CompilerPath = '',
    [string]$NetStandardReferencePath = '',
    [string]$InputSystemAssemblyPath = '',
    [switch]$AllowUnity2022CompatibilityCopy,
    [string]$ReportPath = 'docs/06_MarketModules/offline-type-check.txt'
)

# Offline C# checks only. Never starts Unity or writes to Assets, Packages or ProjectSettings.
# Unity 2022 references and an optional InputSystem stub cannot certify Unity 6000 compatibility.
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetRoot = Split-Path -Parent $dotnet
$report = New-Object 'System.Collections.Generic.List[string]'
$failed = $false
$utf8 = New-Object Text.UTF8Encoding($false)
$scratch = Join-Path $RepositoryRoot '.local/offline-type-check'
$assetRoot = Join-Path $RepositoryRoot 'unity_prototype/BorrowingSun/Assets/BorrowingSun'

function Local-Output([string]$RelativePath) {
    $path = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
    if (!$path.StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Output path must stay within this repository.'
    }
    return $path
}

function Compiler-Check([string]$Name, [string[]]$Sources, [string[]]$References, [string]$Defines = '', [switch]$AllowFailure) {
    if ($Sources.Count -eq 0) { throw ($Name + ': no source files were found.') }
    $assembly = Join-Path $scratch ($Name + '.dll')
    $response = Join-Path $scratch ($Name + '.rsp')
    $arguments = New-Object 'System.Collections.Generic.List[string]'
    foreach ($flag in @('/nologo', '/nostdlib+', '/target:library', '/langversion:9.0', '/deterministic+', '/warn:4', '/utf8output', '/preferreduilang:en-US')) {
        $arguments.Add($flag)
    }
    $arguments.Add('/out:"' + $assembly + '"')
    if ($Defines) { $arguments.Add('/define:' + $Defines) }
    foreach ($reference in ($References | Sort-Object -Unique)) { $arguments.Add('/reference:"' + $reference + '"') }
    foreach ($source in ($Sources | Sort-Object -Unique)) { $arguments.Add('"' + $source + '"') }
    [IO.File]::WriteAllText($response, ($arguments -join "`n") + "`n", $utf8)

    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $dotnet
    $start.Arguments = '"' + $CompilerPath + '" /noconfig "@' + $response + '"'
    $start.WorkingDirectory = $RepositoryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = $utf8
    $start.StandardErrorEncoding = $utf8
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $diagnostics = ($stdout.Result + $stderr.Result).Trim()
    [IO.File]::WriteAllText((Join-Path $scratch ($Name + '.log')), $diagnostics + "`n", $utf8)
    $warnings = [regex]::Matches($diagnostics, '\bwarning CS\d+:').Count
    $errors = [regex]::Matches($diagnostics, '\berror CS\d+:').Count
    $status = if ($process.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }
    $report.Add($status + ' ' + $Name + ': ' + $Sources.Count + ' C# files; ' + $errors + ' errors; ' + $warnings + ' warnings.')
    foreach ($line in ($diagnostics -split "`n")) {
        if ($line -match '\berror CS\d+:') { $report.Add($line.Trim().Replace($RepositoryRoot + '\', '').Replace($RepositoryRoot + '/', '')) }
    }
    foreach ($group in ([regex]::Matches($diagnostics, '\bwarning (CS\d+):') | ForEach-Object { $_.Groups[1].Value } | Group-Object)) {
        $report.Add('Warning summary: ' + $group.Name + ' x ' + $group.Count + '. Full diagnostic log: .local/offline-type-check/' + $Name + '.log')
    }
    if ($process.ExitCode -ne 0) {
        if ($AllowFailure) { return '' }
        throw ($Name + ' compiler exited with code ' + $process.ExitCode + '.')
    }
    return $assembly
}

try {
    [IO.Directory]::CreateDirectory($scratch) | Out-Null
    if (!$CompilerPath) {
        $sdk = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'sdk') -Directory |
            Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
            Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        $CompilerPath = Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
    }
    if (!$NetStandardReferencePath) {
        $NetStandardReferencePath = Join-Path $dotnetRoot 'packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1'
    }
    if (!(Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'Roslyn csc.dll was not found; pass -CompilerPath.' }
    $standardRefs = @(Get-ChildItem -LiteralPath $NetStandardReferencePath -Filter '*.dll' | ForEach-Object FullName)
    if ($standardRefs.Count -eq 0) { throw 'No .NET Standard 2.1 reference assemblies were found.' }
    $projectVersion = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'unity_prototype/BorrowingSun/ProjectSettings/ProjectVersion.txt') | Select-Object -First 1
    $report.Add('Unity Market Modules: offline C# type checks')
    $report.Add('Project: ' + $projectVersion)
    $report.Add('Compiler: ' + $CompilerPath)
    $report.Add('Language/framework: C# 9.0; /nostdlib+; .NET Standard 2.1 reference assemblies.')
    $report.Add('This script does not launch Unity. No import, Unity 6000 compilation or PlayMode result is claimed.')

    $domainSources = @(Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Runtime/Market/Domain') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    [void](Compiler-Check 'Market.Domain' $domainSources $standardRefs)
    $report.Add('Domain was compiled with zero Unity or Godot references.')

    $engineFolder = Join-Path $UnityManagedPath 'UnityEngine'
    $engineRefs = @(Get-ChildItem -LiteralPath $engineFolder -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
    $editorRefs = @(Get-ChildItem -LiteralPath $engineFolder -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
    if (!$engineRefs.Count -or !$editorRefs.Count) { throw 'Unity Engine/Editor reference assemblies were not found.' }
    $report.Add('Engine/editor references: ' + $UnityManagedPath)
    $report.Add('These installed Unity 2022 references differ from the project Unity version.')

    if ($InputSystemAssemblyPath) {
        $InputSystemAssemblyPath = [IO.Path]::GetFullPath($InputSystemAssemblyPath)
        if (!(Test-Path -LiteralPath $InputSystemAssemblyPath -PathType Leaf)) { throw 'Specified InputSystem assembly does not exist.' }
        $report.Add('InputSystem reference: ' + $InputSystemAssemblyPath)
    } else {
        $report.Add('InputSystem: minimal handwritten compile-only stub. InputSystem API/package compatibility and input behavior are NOT validated.')
        $stub = @'
// COMPILE-ONLY approximation. Never add this file/assembly to Unity Assets or execute it.
using System;
using UnityEngine;
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl
    {
        public bool isPressed => throw new NotSupportedException("Compile-only stub");
        public bool wasPressedThisFrame => throw new NotSupportedException("Compile-only stub");
        public bool wasReleasedThisFrame => throw new NotSupportedException("Compile-only stub");
    }
    public class KeyControl : ButtonControl { }
    public class Vector2Control
    {
        public Vector2 ReadValue() => throw new NotSupportedException("Compile-only stub");
    }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    public class Keyboard
    {
        public static Keyboard current => throw new NotSupportedException("Compile-only stub");
        public KeyControl aKey, cKey, dKey, eKey, fKey, nKey, qKey, rKey, sKey, vKey, wKey, xKey, zKey;
        public KeyControl digit1Key, digit2Key, digit3Key, escapeKey, tabKey, spaceKey, f2Key;
        public KeyControl leftCtrlKey, rightCtrlKey, leftArrowKey, rightArrowKey, upArrowKey, downArrowKey;
    }
    public class Mouse
    {
        public static Mouse current => throw new NotSupportedException("Compile-only stub");
        public ButtonControl leftButton, rightButton;
        public Vector2Control delta, position, scroll;
    }
}
'@
        $stubPath = Join-Path $scratch 'InputSystem.CompileOnly.cs'
        [IO.File]::WriteAllText($stubPath, $stub, $utf8)
        $InputSystemAssemblyPath = Compiler-Check 'InputSystem.CompileOnly' @($stubPath) ($standardRefs + $engineRefs)
    }

    $runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Runtime') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    $runtimeRefs = $standardRefs + $engineRefs + $editorRefs + @($InputSystemAssemblyPath)
    $defines = 'UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_2022_3_OR_NEWER;UNITY_5_3_OR_NEWER;ENABLE_INPUT_SYSTEM'
    $runtimeAssembly = Compiler-Check 'BorrowingSun.Runtime' $runtimeSources $runtimeRefs $defines -AllowFailure:$AllowUnity2022CompatibilityCopy
    if (!$runtimeAssembly -and $AllowUnity2022CompatibilityCopy) {
        $runtimeLog = [IO.File]::ReadAllText((Join-Path $scratch 'BorrowingSun.Runtime.log'), $utf8)
        $errorLines = @($runtimeLog -split "`n" | Where-Object { $_ -match '\berror CS\d+:' })
        $knownErrors = @($errorLines | Where-Object { $_ -match 'SkywellWeatherZone\.cs\(\d+,\d+\): error CS1501:.*FindObjectsByType' })
        if ($errorLines.Count -ne 2 -or $knownErrors.Count -ne 2) {
            throw 'The failed compilation contains errors other than the two known Unity 2022 overload differences. No compatibility copy was used.'
        }
        $weatherPath = Join-Path $assetRoot 'Runtime/Skywell/SkywellWeatherZone.cs'
        $weatherText = [IO.File]::ReadAllText($weatherPath, $utf8)
        $oldCall = 'FindObjectsByType<SkywellWeatherZone>()'
        if ([regex]::Matches($weatherText, [regex]::Escape($oldCall)).Count -ne 2) {
            throw 'Expected exactly two old weather-zone calls; refusing an unspecified compatibility rewrite.'
        }
        $compatibilityPath = Join-Path $scratch 'SkywellWeatherZone.Unity2022.CompileOnly.cs'
        [IO.File]::WriteAllText($compatibilityPath,
            $weatherText.Replace($oldCall, 'FindObjectsByType<SkywellWeatherZone>(FindObjectsSortMode.None)'), $utf8)
        $report.Add('COMPATIBILITY COPY: the unmodified Runtime compilation above failed on two existing overload calls absent from the installed Unity 2022 references.')
        $report.Add('Retry replaces those two calls only in a .local copy of SkywellWeatherZone.cs with an explicit FindObjectsSortMode.None argument. Production sources remain untouched.')
        $compatibilitySources = @($runtimeSources | Where-Object { $_ -ne $weatherPath }) + @($compatibilityPath)
        $runtimeAssembly = Compiler-Check 'BorrowingSun.Runtime.CompatibilityCopy' $compatibilitySources $runtimeRefs $defines
    }
    $editorSources = @(Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Editor/Market') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    [void](Compiler-Check 'Market.Editor' $editorSources ($runtimeRefs + @($runtimeAssembly)) $defines)
    $report.Add('PASS Offline type checks completed. Generated assemblies are compile artifacts only and must not be run or imported into Unity.')
} catch {
    $failed = $true
    $report.Add('ERROR ' + $_.Exception.Message)
} finally {
    $report.Add('Limits: does not prove Unity 6000/package compatibility, scene loading, lifecycle, input, physics, UI or gameplay behavior.')
    $reproduce = 'Reproduce: powershell -NoProfile -File Tools/check-unity-runtime-compile.ps1'
    if ($AllowUnity2022CompatibilityCopy) { $reproduce += ' -AllowUnity2022CompatibilityCopy' }
    $report.Add($reproduce + ' (override reference paths as needed).')
    $absoluteReport = Local-Output $ReportPath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absoluteReport)) | Out-Null
    [IO.File]::WriteAllText($absoluteReport, ($report -join "`n") + "`n", $utf8)
    $report | Write-Output
}
if ($failed) { exit 1 }
