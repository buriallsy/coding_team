[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Baseline = '5b83325204060e1312c46f36f61e5489f6a9cf70',
    [string]$ReportPath = 'docs/06_MarketModules/static-validation.txt'
)

# This checks serialized assets without launching Unity. It is not a PlayMode test.
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$git = (Get-Command git -ErrorAction Stop).Source
$report = New-Object 'System.Collections.Generic.List[string]'
$failures = New-Object 'System.Collections.Generic.List[string]'
$script:passed = 0
$sceneDirectory = 'unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/'
$sourceScene = $sceneDirectory + '05_Skywell_FlowerMarket.unity'
$newScene = $sceneDirectory + '05_Skywell_MarketModules.unity'
$oldMarketId = '83344675'
$controllerObjectId = '887768185'
$moduleId = '9000000000001'
$hudId = '9000000000002'
$moduleGuid = 'ee7688313447403d93b140a2ea5b6128'

function Read-Git([string[]]$Arguments) {
    # Paths in this repository contain no embedded quotes. Each argument is quoted separately.
    foreach ($arg in $Arguments) { if ($arg.Contains('"')) { throw 'Unexpected quote in Git argument.' } }
    $quoted = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $git
    $start.Arguments = $quoted
    $start.WorkingDirectory = $RepositoryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = New-Object Text.UTF8Encoding($false)
    $process = [Diagnostics.Process]::Start($start)
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw ('git ' + $quoted + ': ' + $errorTask.Result.Trim()) }
    return $outputTask.Result
}

function Check([bool]$Condition, [string]$Message) {
    if ($Condition) {
        $script:passed++
        $report.Add(('PASS ' + $Message).TrimEnd())
    } else {
        $failures.Add($Message)
        $report.Add(('FAIL ' + $Message).TrimEnd())
    }
}

function Read-Local([string]$RelativePath) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
    if (!$absolute.StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Path leaves repository: ' + $RelativePath)
    }
    return [IO.File]::ReadAllText($absolute, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
}

function Scene-Blocks([string]$Text) {
    $blocks = @{}
    $pattern = '(?ms)^--- !u!(?<class>\d+) &(?<id>-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)'
    foreach ($match in [regex]::Matches($Text, $pattern)) {
        $id = $match.Groups['id'].Value
        if ($blocks.ContainsKey($id)) { throw ('Duplicate serialized fileID: ' + $id) }
        $blocks[$id] = @{ Class = $match.Groups['class'].Value; Text = $match.Value }
    }
    return $blocks
}

try {
    $report.Add('Unity Market Modules: static asset validation')
    $report.Add('Baseline: ' + $Baseline)
    $report.Add('Scope: Git blobs, YAML fileIDs, script references and asset GUIDs.')
    $report.Add('Unity was not launched. This report is not compilation, PlayMode, collision or manual play evidence.')
    [void](Read-Git @('cat-file', '-e', ($Baseline + '^{commit}')))

    $protected = @(
        ($sceneDirectory + '05_Skywell.unity'),
        ($sceneDirectory + '05_Skywell.unity.meta'),
        $sourceScene,
        ($sourceScene + '.meta'),
        'unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVoyage.cs',
        'unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVoyage.cs.meta'
    )
    foreach ($path in $protected) {
        $expected = (Read-Git @('rev-parse', ($Baseline + ':' + $path))).Trim()
        $actual = (Read-Git @('hash-object', ('--path=' + $path), '--', $path)).Trim()
        Check ($expected -eq $actual) ('Protected asset matches baseline Git blob: ' + $path)
    }

    $baselineText = (Read-Git @('show', ($Baseline + ':' + $sourceScene))).Replace("`r`n", "`n")
    $newText = Read-Local $newScene
    $before = Scene-Blocks $baselineText
    $after = Scene-Blocks $newText
    Check ($before.Count -gt 0) ('Baseline scene parsed: ' + $before.Count + ' objects/components')
    $missing = @($before.Keys | Where-Object { !$after.ContainsKey($_) })
    Check ($missing.Count -eq 0) ('All baseline fileIDs retained; missing: ' + ($missing -join ', '))
    $added = @($after.Keys | Where-Object { !$before.ContainsKey($_) })
    $allowedNew = @($moduleId, $hudId)
    Check ($added.Count -ge 1 -and $added.Count -le 2 -and @($added | Where-Object { $_ -notin $allowedNew }).Count -eq 0) ('Only module/HUD component fileIDs added: ' + (($added | Sort-Object) -join ', '))
    Check ($after.ContainsKey($moduleId)) 'New market module component is present'

    $unexpectedChanges = New-Object 'System.Collections.Generic.List[string]'
    $unchanged = 0
    foreach ($id in $before.Keys) {
        if (!$after.ContainsKey($id)) { continue }
        $old = $before[$id].Text
        $current = $after[$id].Text
        if ($id -eq $oldMarketId) {
            $expected = [regex]::Replace($old, '(?m)^  m_Enabled: 1$', '  m_Enabled: 0')
            Check ($expected -ne $old -and $current -eq $expected) 'Legacy Flower Market component is only disabled'
        } elseif ($id -eq $controllerObjectId) {
            foreach ($newId in $added) {
                $current = [regex]::Replace($current, '(?m)^  - component: \{fileID: ' + [regex]::Escape($newId) + '\}\n', '')
            }
            Check ($current -eq $old) 'Controller GameObject only gains module/HUD component references'
            foreach ($newId in $added) {
                $matches = [regex]::Matches($after[$id].Text, '(?m)^  - component: \{fileID: ' + [regex]::Escape($newId) + '\}$')
                Check ($matches.Count -eq 1) ('New component is attached exactly once: ' + $newId)
            }
        } elseif ($current -ne $old) {
            $unexpectedChanges.Add($id)
        } else { $unchanged++ }
    }
    Check ($unexpectedChanges.Count -eq 0) ('Other original YAML blocks unchanged: ' + $unchanged + '; unexpected changes: ' + ($unexpectedChanges -join ', '))

    $metaPaths = @(Read-Git @('ls-files', '--cached', '--others', '--exclude-standard', '--', '*.meta')) -split "`n" | Where-Object { $_ }
    $byGuid = @{}
    $byPath = @{}
    foreach ($path in ($metaPaths | Sort-Object -Unique)) {
        $match = [regex]::Match((Read-Local $path), '(?m)^guid: ([0-9a-fA-F]{32})\s*$')
        if (!$match.Success) { Check $false ('Meta has a valid GUID: ' + $path); continue }
        $guid = $match.Groups[1].Value.ToLowerInvariant()
        $byPath[$path] = $guid
        if (!$byGuid.ContainsKey($guid)) { $byGuid[$guid] = New-Object 'System.Collections.Generic.List[string]' }
        $byGuid[$guid].Add($path)
    }

    $baselineGuids = @{}
    $baselineLines = (Read-Git @('grep', '-n', '-E', '^guid: [0-9a-fA-F]{32}$', $Baseline, '--', '*.meta')) -split "`n"
    foreach ($line in $baselineLines) {
        $match = [regex]::Match($line, '^[0-9a-f]+:(?<path>.+):\d+:guid: (?<guid>[0-9a-fA-F]{32})\r?$')
        if (!$match.Success) { continue }
        $guid = $match.Groups['guid'].Value.ToLowerInvariant()
        if (!$baselineGuids.ContainsKey($guid)) { $baselineGuids[$guid] = @() }
        $baselineGuids[$guid] += $match.Groups['path'].Value
    }
    $newDuplicates = New-Object 'System.Collections.Generic.List[string]'
    foreach ($guid in $byGuid.Keys) {
        if ($byGuid[$guid].Count -lt 2) { continue }
        $oldPaths = @($baselineGuids[$guid])
        if ($oldPaths.Count -lt 2 -or @($byGuid[$guid] | Where-Object { $_ -notin $oldPaths }).Count -gt 0) {
            $newDuplicates.Add($guid + ' [' + ($byGuid[$guid] -join ', ') + ']')
        }
    }
    Check ($newDuplicates.Count -eq 0) ('No newly duplicated asset GUIDs across ' + $byPath.Count + ' meta files: ' + ($newDuplicates -join '; '))

    foreach ($id in $added) {
        $block = $after[$id]
        Check ($block.Class -eq '114') ('Added fileID is a MonoBehaviour: ' + $id)
        Check ($block.Text -match '(?m)^  m_GameObject: \{fileID: 887768185\}$') ('Added component uses the existing Controller object: ' + $id)
        $scriptMatch = [regex]::Match($block.Text, 'm_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32}), type: 3\}')
        Check $scriptMatch.Success ('Added component has a serialized script GUID: ' + $id)
        if (!$scriptMatch.Success) { continue }
        $guid = $scriptMatch.Groups[1].Value.ToLowerInvariant()
        $paths = @()
        if ($byGuid.ContainsKey($guid)) { $paths = @($byGuid[$guid]) }
        Check ($paths.Count -eq 1 -and $paths[0].EndsWith('.cs.meta')) ('Added script GUID resolves to exactly one C# meta: ' + $guid)
        if ($id -eq $moduleId) { Check ($guid -eq $moduleGuid) 'Market module uses its declared GUID' }
        if ($paths.Count -eq 1) {
            $sourcePath = $paths[0].Substring(0, $paths[0].Length - 5)
            Check (Test-Path -LiteralPath (Join-Path $RepositoryRoot $sourcePath) -PathType Leaf) ('Added C# script exists: ' + $sourcePath)
        }
    }

    Check ($byPath.ContainsKey($newScene + '.meta')) 'New scene has its own meta file'
    if ($byPath.ContainsKey($newScene + '.meta')) {
        $newGuid = $byPath[$newScene + '.meta']
        Check ($newGuid -ne $byPath[$sourceScene + '.meta'] -and $byGuid[$newGuid].Count -eq 1) 'New scene GUID is distinct and unique'
    }
    $report.Add('Baseline GUID duplicate groups retained: ' + @($baselineGuids.Keys | Where-Object { $baselineGuids[$_].Count -gt 1 }).Count)
} catch {
    $failures.Add($_.Exception.Message)
    $report.Add('ERROR ' + $_.Exception.Message)
} finally {
    $report.Add('Static checks passed: ' + $script:passed + '; failures: ' + $failures.Count)
    $report.Add('Limits: does not prove Unity import, compilation, PlayMode, visual layout, walking routes or gameplay correctness.')
    $absoluteReport = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $ReportPath))
    if (!$absoluteReport.StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Report path leaves repository.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absoluteReport)) | Out-Null
    [IO.File]::WriteAllText($absoluteReport, ($report -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
    $report | Write-Output
}
if ($failures.Count -gt 0) { exit 1 }
