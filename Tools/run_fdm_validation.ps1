[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$PythonPath = "python",
    [string]$ResultsDir,
    [int]$UnityTimeoutSeconds = 1500,
    [int]$PythonTimeoutSeconds = 300,
    [switch]$RunMutations
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Script:Manifest = $null
$Script:RepoRoot = $null
$Script:UnityExe = $null
$Script:PythonExe = $null
$Script:InitialStatus = @()
$Script:SuiteResults = [System.Collections.Generic.List[object]]::new()
$Script:MutationResults = [System.Collections.Generic.List[object]]::new()
$Script:StartedUtc = [DateTime]::UtcNow
$Script:ExitCode = 0
$Script:OverallStatus = "NOT_RUN"

function Invoke-GitText {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed ($LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return (($output -join "`n").Trim())
}

function Get-GitStatusLines {
    $lines = & git -C $Script:RepoRoot status --porcelain=v1 --untracked-files=all 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git status failed: $($lines -join ' ')" }
    return @($lines | Where-Object { $_ -ne $null -and $_.Length -gt 0 })
}

function Get-StatusPath {
    param([string]$Line)
    if ($Line.Length -lt 4) { return $Line }
    $path = $Line.Substring(3)
    if ($path -match ' -> ') { $path = ($path -split ' -> ')[-1] }
    return $path.Trim('"') -replace '\\','/'
}

function Assert-OnlyImplementationDirty {
    param([string[]]$StatusLines)
    $allowed = @{}
    foreach ($p in $Script:Manifest.implementation_paths) { $allowed[[string]$p] = $true }
    $unexpected = New-Object System.Collections.Generic.List[string]
    foreach ($line in $StatusLines) {
        $p = Get-StatusPath $line
        if (-not $allowed.ContainsKey($p)) { $unexpected.Add("$line") }
    }
    if ($unexpected.Count -gt 0) {
        throw "unexpected dirty checkout paths (only the six Baseline v1 candidate files may be dirty):`n$($unexpected -join "`n")"
    }
}

function Assert-StatusUnchanged {
    $now = @(Get-GitStatusLines | Sort-Object)
    $before = @($Script:InitialStatus | Sort-Object)
    $delta = Compare-Object -ReferenceObject $before -DifferenceObject $now
    if ($delta) {
        throw "checkout dirty-state changed during validation:`n$($delta | Out-String)"
    }
}

function Assert-AuthorityAndInventory {
    $head = Invoke-GitText @('-C',$Script:RepoRoot,'rev-parse','HEAD')

    # The authority commit freezes the validation inventory and participating validator blobs; it is
    # not required to be the checkout HEAD. Requiring exact HEAD made the runner impossible to track:
    # committing the runner itself necessarily moves HEAD away from the frozen authority. Descendant
    # checkouts are allowed, while the per-surface blob checks below still reject any drift in the
    # validation authority itself.
    $ancestor = & git -C $Script:RepoRoot merge-base --is-ancestor $Script:Manifest.authority_commit HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "authority commit $($Script:Manifest.authority_commit) is not an ancestor of HEAD $head"
    }

    $roots = @($Script:Manifest.inventory.authority_cs_roots | ForEach-Object { [string]$_ })
    $authorityCs = @(& git -C $Script:RepoRoot ls-tree -r --name-only $Script:Manifest.authority_commit -- @roots |
        Where-Object { $_ -like '*.cs' } | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw "git ls-tree failed while enumerating C# validation surfaces" }
    $manifestCs = @($Script:Manifest.surfaces | Where-Object { $_.path -like '*.cs' } |
        ForEach-Object { [string]$_.path } | Sort-Object)
    $csDelta = Compare-Object -ReferenceObject $authorityCs -DifferenceObject $manifestCs
    if ($csDelta) {
        throw "missing/unlisted C# validation surface(s):`n$($csDelta | Out-String)"
    }
    if ($authorityCs.Count -ne [int]$Script:Manifest.inventory.expected_authority_cs_surface_count) {
        throw "authority C# surface count drift: expected $($Script:Manifest.inventory.expected_authority_cs_surface_count), found $($authorityCs.Count)"
    }

    # HEAD enumeration. Allowing descendant checkouts (so the runner can be committed at all) opened
    # a hole: enumerating only at the authority commit means a validation/editor surface ADDED after
    # it is invisible to every integrity check here. MavSixDoFBodyInspector.cs entered
    # FlightDynamics/Editor exactly that way. The declared roots are therefore enumerated at HEAD as
    # well, and an unlisted surface is a hard failure until the manifest accounts for it - as an
    # executed suite or as EXCLUDED_WITH_REASON, but never by silence.
    $headCs = @(& git -C $Script:RepoRoot ls-tree -r --name-only HEAD -- @roots |
        Where-Object { $_ -like '*.cs' } | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw "git ls-tree failed while enumerating C# validation surfaces at HEAD" }
    $headDelta = Compare-Object -ReferenceObject $headCs -DifferenceObject $manifestCs
    if ($headDelta) {
        $added = @($headDelta | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object { $_.InputObject })
        $removed = @($headDelta | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object { $_.InputObject })
        $msg = "C# validation surface drift between HEAD and the manifest."
        if ($added.Count -gt 0) {
            $msg += "`n  present at HEAD but unlisted (add them to the manifest, executed or EXCLUDED_WITH_REASON):`n    " + ($added -join "`n    ")
        }
        if ($removed.Count -gt 0) {
            $msg += "`n  listed in the manifest but absent at HEAD:`n    " + ($removed -join "`n    ")
        }
        throw $msg
    }

    $toolFiles = @(& git -C $Script:RepoRoot ls-tree -r --name-only $Script:Manifest.authority_commit -- Tools |
        Where-Object { $_ -like 'Tools/validate_f16_tp1538_*.py' } | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw "git ls-tree failed while enumerating Python validators" }
    $manifestPy = @($Script:Manifest.surfaces | Where-Object { $_.path -like '*.py' } |
        ForEach-Object { [string]$_.path } | Sort-Object)
    $pyDelta = Compare-Object -ReferenceObject $toolFiles -DifferenceObject $manifestPy
    if ($pyDelta) {
        throw "missing/unlisted TP-1538 Python validator(s):`n$($pyDelta | Out-String)"
    }
    if ($toolFiles.Count -ne [int]$Script:Manifest.inventory.expected_python_surface_count) {
        throw "authority Python validator count drift: expected $($Script:Manifest.inventory.expected_python_surface_count), found $($toolFiles.Count)"
    }

    # Same HEAD enumeration for the Python validators, for the same reason.
    $headPy = @(& git -C $Script:RepoRoot ls-tree -r --name-only HEAD -- Tools |
        Where-Object { $_ -like 'Tools/validate_f16_tp1538_*.py' } | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw "git ls-tree failed while enumerating Python validators at HEAD" }
    $headPyDelta = Compare-Object -ReferenceObject $headPy -DifferenceObject $manifestPy
    if ($headPyDelta) {
        throw "TP-1538 Python validator drift between HEAD and the manifest:`n$($headPyDelta | Out-String)"
    }

    foreach ($surface in $Script:Manifest.surfaces) {
        $path = [string]$surface.path
        $full = Join-Path $Script:RepoRoot ($path -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw "tracked validation surface missing from checkout: $path"
        }
        $blob = Invoke-GitText @('-C',$Script:RepoRoot,'hash-object','--',$path)
        if ($blob -ne [string]$surface.git_blob) {
            throw "participating source hash mismatch for $path. expected $($surface.git_blob), got $blob"
        }
        if (@('OFFLINE_ELIGIBLE','UNITY_REQUIRED','EXCLUDED_WITH_REASON') -notcontains [string]$surface.classification) {
            throw "invalid classification for ${path}: $($surface.classification)"
        }
        if ([string]$surface.classification -eq 'EXCLUDED_WITH_REASON' -and [string]::IsNullOrWhiteSpace([string]$surface.reason)) {
            throw "excluded surface lacks reason: $path"
        }
    }
}

function Resolve-PythonExecutable {
    $cmd = Get-Command $PythonPath -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { throw "Python not found: $PythonPath" }
    $exe = $cmd.Source
    $probe = Invoke-ExternalProcess -FilePath $exe -Arguments @('--version') -TimeoutSeconds 30 -Tag 'python-version'
    if ($probe.TimedOut -or $probe.ExitCode -ne 0) { throw "Python exists but could not execute successfully: $exe" }
    return $exe
}

function Resolve-UnityExecutable {
    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity executable not found: $UnityPath" }
        return (Resolve-Path -LiteralPath $UnityPath).Path
    }
    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EXE)) {
        if (-not (Test-Path -LiteralPath $env:UNITY_EXE -PathType Leaf)) { throw "UNITY_EXE does not exist: $env:UNITY_EXE" }
        return (Resolve-Path -LiteralPath $env:UNITY_EXE).Path
    }

    $projectVersion = Join-Path $Script:RepoRoot 'ProjectSettings/ProjectVersion.txt'
    if (Test-Path -LiteralPath $projectVersion) {
        $line = Get-Content -LiteralPath $projectVersion | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
        if ($line -match '^m_EditorVersion:\s*(.+)$') {
            $v = $Matches[1].Trim()
            $candidate = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$v/Editor/Unity.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    throw "Unity not found. Supply -UnityPath or set UNITY_EXE. The runner will not guess a different editor version."
}

function ConvertTo-NativeArgument {
    param([string]$Value)
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value -replace '"','\"') + '"'
}

function Invoke-ExternalProcess {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [Parameter(Mandatory=$true)][int]$TimeoutSeconds,
        [Parameter(Mandatory=$true)][string]$Tag,
        [string]$WorkingDirectory = $Script:RepoRoot
    )
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = (($Arguments | ForEach-Object { ConvertTo-NativeArgument ([string]$_) }) -join ' ')

    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    $started = [DateTime]::UtcNow
    if (-not $p.Start()) { throw "could not start $Tag" }
    $stdoutTask = $p.StandardOutput.ReadToEndAsync()
    $stderrTask = $p.StandardError.ReadToEndAsync()
    $timedOut = -not $p.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) {
        try { $p.Kill() } catch {}
        try { $p.WaitForExit() } catch {}
    } else {
        $p.WaitForExit()
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $exit = if ($timedOut) { -1 } else { $p.ExitCode }
    $p.Dispose()
    return [pscustomobject]@{
        Tag = $Tag
        ExitCode = $exit
        TimedOut = $timedOut
        StdOut = $stdout
        StdErr = $stderr
        DurationMs = [int]([DateTime]::UtcNow - $started).TotalMilliseconds
    }
}

function Test-UnityCompileFailure {
    param([string]$LogText)
    if ([string]::IsNullOrEmpty($LogText)) { return $false }
    return ($LogText -match '(?im)\berror\s+CS\d{4}\b' -or
            $LogText -match '(?im)Scripts have compiler errors' -or
            $LogText -match '(?im)Compilation failed' -or
            $LogText -match '(?im)compile errors?')
}

function Parse-LegacyResultMarker {
    param([string]$Text)
    $matches = [regex]::Matches($Text, 'RESULT:\s*(PASS|FAIL)\s+passed\s*=\s*(\d+)\s+failed\s*=\s*(\d+)', 'IgnoreCase')
    if ($matches.Count -eq 0) { return $null }
    $m = $matches[$matches.Count - 1]
    return [pscustomobject]@{
        Status = $m.Groups[1].Value.ToUpperInvariant()
        Passed = [int]$m.Groups[2].Value
        Failed = [int]$m.Groups[3].Value
    }
}

function New-SuiteResult {
    param($Surface,[string]$Status,[Nullable[int]]$Passed,[Nullable[int]]$Failed,[Nullable[int]]$ExitCode,[int]$DurationMs,[string]$Detail,[string]$LogFile,[string]$ResultFile)
    $expected = $null
    if ($Surface.PSObject.Properties.Name -contains 'source_expected_assertions') { $expected = [int]$Surface.source_expected_assertions }
    $cardinality = $null
    if ($null -ne $expected -and $null -ne $Passed -and $null -ne $Failed) { $cardinality = (($Passed + $Failed) -eq $expected) }
    return [pscustomobject]@{
        id = [string]$Surface.id
        path = [string]$Surface.path
        classification = [string]$Surface.classification
        counted = [bool]$Surface.counted
        status = $Status
        passed = $Passed
        failed = $Failed
        exit_code = $ExitCode
        duration_ms = $DurationMs
        source_expected_assertions = $expected
        source_cardinality_match = $cardinality
        detail = $Detail
        log_file = $LogFile
        result_file = $ResultFile
    }
}

function Invoke-PythonSurface {
    param($Surface,[string]$ProjectRoot,[string]$OutputRoot)
    $safe = ([string]$Surface.id -replace '[^A-Za-z0-9_.-]','_')
    $log = Join-Path $OutputRoot "$safe.python.log"
    $path = Join-Path $ProjectRoot (([string]$Surface.path) -replace '/', [IO.Path]::DirectorySeparatorChar)
    $args = @($path)
    if ($Surface.execution.PSObject.Properties.Name -contains 'args') { $args += @($Surface.execution.args | ForEach-Object { [string]$_ }) }
    $proc = Invoke-ExternalProcess -FilePath $Script:PythonExe -Arguments $args -TimeoutSeconds $PythonTimeoutSeconds -Tag $Surface.id -WorkingDirectory $ProjectRoot
    $combined = $proc.StdOut + "`n" + $proc.StdErr
    [IO.File]::WriteAllText($log, $combined, (New-Object Text.UTF8Encoding($false)))
    if ($proc.TimedOut) { return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $null $proc.DurationMs 'Python validator timed out' $log $null }

    $marker = Parse-LegacyResultMarker $combined
    $passed = $null; $failed = $null
    $status = if ($proc.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }
    $detail = "Python exit code $($proc.ExitCode); stdout/stderr captured"
    if ($null -ne $marker) {
        $passed = [int]$marker.Passed; $failed = [int]$marker.Failed
        if ($marker.Status -eq 'FAIL' -or $failed -gt 0) { $status = 'FAIL' }
        if ($proc.ExitCode -eq 0 -and $status -eq 'FAIL') { $detail += '; result marker reports failure despite exit 0' }
    }
    return New-SuiteResult $Surface $status $passed $failed $proc.ExitCode $proc.DurationMs $detail $log $null
}

function Invoke-UnitySurface {
    param($Surface,[string]$ProjectRoot,[string]$OutputRoot,[string]$NameSuffix='')
    $safe = (([string]$Surface.id + $NameSuffix) -replace '[^A-Za-z0-9_.-]','_')
    $log = Join-Path $OutputRoot "$safe.unity.log"
    $kind = [string]$Surface.execution.kind
    $resultPath = Join-Path $OutputRoot "$safe.result"
    $args = @('-batchmode','-nographics','-projectPath',$ProjectRoot,'-logFile',$log)

    if ($kind -eq 'unity_existing_batch') {
        $args += @('-executeMethod',[string]$Surface.execution.execute_method,[string]$Surface.execution.result_arg,$resultPath)
    } elseif ($kind -eq 'unity_scheduler_adapter') {
        $args += @('-executeMethod','MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch',
                   '-fdmMode','scheduler','-fdmSuite',[string]$Surface.id,'-fdmOut',$resultPath)
    } elseif ($kind -eq 'unity_sync_adapter') {
        $args += @('-executeMethod','MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch',
                   '-fdmMode','sync','-fdmSuite',[string]$Surface.id,'-fdmType',[string]$Surface.execution.type,
                   '-fdmMethod',[string]$Surface.execution.method,'-fdmOut',$resultPath)
    } else {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $null 0 "unsupported Unity execution kind: $kind" $log $resultPath
    }

    $slnxPath = Join-Path $Script:RepoRoot 'Maverick.slnx'
    $slnxOriginalBytes = $null

    if (Test-Path -LiteralPath $slnxPath -PathType Leaf) {
        $slnxOriginalBytes = [IO.File]::ReadAllBytes($slnxPath)
    }

    try {
        $proc = Invoke-ExternalProcess -FilePath $Script:UnityExe -Arguments $args -TimeoutSeconds $UnityTimeoutSeconds -Tag $Surface.id -WorkingDirectory $ProjectRoot
    }
    finally {
        if ($null -ne $slnxOriginalBytes) {
            [IO.File]::WriteAllBytes($slnxPath, $slnxOriginalBytes)
        }
    }
    $logText = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
    if ($proc.TimedOut) { return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $null $proc.DurationMs 'Unity validator timed out' $log $resultPath }
    if (Test-UnityCompileFailure $logText) { return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs 'Unity compile/import failure detected; not a validator result' $log $resultPath }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs 'missing result file/result marker' $log $resultPath
    }

    $passed = $null; $failed = $null; $status = 'INFRA_FAILURE'; $detail = ''
    if ($kind -eq 'unity_existing_batch') {
        $text = Get-Content -LiteralPath $resultPath -Raw
        $marker = Parse-LegacyResultMarker $text
        if ($null -eq $marker) {
            return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs 'malformed/missing RESULT marker in existing batch output' $log $resultPath
        }
        $passed = [int]$marker.Passed; $failed = [int]$marker.Failed; $status = [string]$marker.Status
        $detail = 'existing batch path reused; runtime RESULT marker authoritative'
    } else {
        try { $record = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json }
        catch { return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs "malformed adapter JSON: $($_.Exception.Message)" $log $resultPath }
        if ([int]$record.schema_version -ne 1 -or [string]::IsNullOrWhiteSpace([string]$record.status)) {
            return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs 'adapter result schema invalid' $log $resultPath
        }
        $status = [string]$record.status
        if ([int]$record.passed -ge 0) { $passed = [int]$record.passed }
        if ([int]$record.failed -ge 0) { $failed = [int]$record.failed }
        $detail = [string]$record.detail
    }

    if ($status -eq 'PASS' -and $proc.ExitCode -ne 0) {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $passed $failed $proc.ExitCode $proc.DurationMs 'result says PASS but Unity exited nonzero' $log $resultPath
    }
    if ($status -eq 'FAIL' -and $proc.ExitCode -eq 0) {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $passed $failed $proc.ExitCode $proc.DurationMs 'result says FAIL but Unity exited 0' $log $resultPath
    }
    if ($status -eq 'INFRA_FAILURE') {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $passed $failed $proc.ExitCode $proc.DurationMs $detail $log $resultPath
    }
    if (@('PASS','FAIL') -notcontains $status) {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $passed $failed $proc.ExitCode $proc.DurationMs "unknown result status: $status" $log $resultPath
    }
    if ($null -eq $passed -or $null -eq $failed) {
        return New-SuiteResult $Surface 'INFRA_FAILURE' $null $null $proc.ExitCode $proc.DurationMs 'counted Unity suite did not report passed/failed counts' $log $resultPath
    }

    $result = New-SuiteResult $Surface $status $passed $failed $proc.ExitCode $proc.DurationMs $detail $log $resultPath
    if ($result.source_expected_assertions -ne $null -and $result.source_cardinality_match -eq $false) {
        $result.status = 'INFRA_FAILURE'
        $result.detail = "runtime count $($passed+$failed) disagrees with deterministic source cardinality $($result.source_expected_assertions); runtime values retained but baseline candidate refuses the drift"
    }
    return $result
}

function Invoke-BaselineSurface {
    param($Surface,[string]$ProjectRoot,[string]$OutputRoot,[string]$NameSuffix='')
    $kind = [string]$Surface.execution.kind
    if ($kind -eq 'python') { return Invoke-PythonSurface $Surface $ProjectRoot $OutputRoot }
    return Invoke-UnitySurface $Surface $ProjectRoot $OutputRoot $NameSuffix
}

function Copy-ImplementationIntoWorkspace {
    param([string]$DestinationRoot)
    foreach ($p in $Script:Manifest.implementation_paths) {
        $src = Join-Path $Script:RepoRoot (([string]$p) -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { throw "implementation file missing while preparing mutation workspace: $p" }
        $dst = Join-Path $DestinationRoot (([string]$p) -replace '/', [IO.Path]::DirectorySeparatorChar)
        $parent = Split-Path -Parent $dst
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
}

function Invoke-Mutations {
    param([hashtable]$BaselineById,[string]$OutputRoot)
    $probes = @($Script:Manifest.mutation_v1.probes)
    if ($probes.Count -eq 0) {
        throw '-RunMutations requested, but mutation_v1 contains zero reviewed NEW probes. Historical 36 are intentionally not imported.'
    }

    foreach ($probe in $probes) {
        $id = [string]$probe.id
        $workspace = Join-Path ([IO.Path]::GetTempPath()) ("maverick-fdm-mut-" + [Guid]::NewGuid().ToString('N'))
        $archive = "$workspace.zip"
        $mutationStatus = 'INFRA_FAILURE'
        $detail = ''
        try {
            & git -C $Script:RepoRoot archive --format=zip --output=$archive $Script:Manifest.authority_commit
            if ($LASTEXITCODE -ne 0) { throw "git archive failed for mutation $id" }
            New-Item -ItemType Directory -Force -Path $workspace | Out-Null
            Expand-Archive -LiteralPath $archive -DestinationPath $workspace -Force
            Copy-ImplementationIntoWorkspace $workspace

            $targetRel = [string]$probe.target_path
            if (-not $targetRel.StartsWith('Assets/MaverickFresh/Scripts/FlightDynamics/', [StringComparison]::Ordinal)) {
                throw "mutation target is outside FlightDynamics production tree: $targetRel"
            }
            if ($targetRel -match '/(Validation|Editor)/') { throw "mutation target is validation infrastructure, not production: $targetRel" }
            $target = Join-Path $workspace ($targetRel -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "mutation target missing: $targetRel" }
            $text = [IO.File]::ReadAllText($target)
            $pre = [string]$probe.preimage
            $replacement = [string]$probe.replacement
            $occurrences = [regex]::Matches($text, [regex]::Escape($pre)).Count
            if ($occurrences -ne 1) { throw "exact-preimage gate failed for ${id}: expected 1 occurrence, found $occurrences" }
            [IO.File]::WriteAllText($target, $text.Replace($pre,$replacement), (New-Object Text.UTF8Encoding($false)))
            if ([IO.File]::ReadAllText($target).Contains($pre)) { throw "mutation preimage still present after replacement: $id" }

            $suiteId = [string]$probe.validator_suite_id
            if (-not $BaselineById.ContainsKey($suiteId)) { throw "mutation references unknown baseline suite: $suiteId" }
            if ([string]$BaselineById[$suiteId].status -ne 'PASS') { throw "baseline validator $suiteId was not PASS; mutation kill cannot be interpreted" }
            $surface = $Script:Manifest.surfaces | Where-Object { $_.id -eq $suiteId } | Select-Object -First 1
            if ($null -eq $surface) { throw "manifest surface missing for mutation validator: $suiteId" }
            $mutOut = Join-Path $OutputRoot ("mutation-" + ($id -replace '[^A-Za-z0-9_.-]','_'))
            New-Item -ItemType Directory -Force -Path $mutOut | Out-Null
            $run = Invoke-BaselineSurface $surface $workspace $mutOut (".mutation.$id")

            if ($run.status -eq 'INFRA_FAILURE') {
                $mutationStatus = 'INFRA_FAILURE'
                $detail = 'mutated project did not successfully compile/import/run the relevant validator; not counted as a kill'
            } elseif ($run.status -eq 'FAIL') {
                $mutationStatus = 'KILLED'
                $detail = 'relevant validator successfully ran and reported semantic failure'
            } elseif ($run.status -eq 'PASS') {
                $mutationStatus = 'SURVIVED'
                $detail = 'relevant validator successfully ran but did not detect the mutation'
            } else {
                $mutationStatus = 'INFRA_FAILURE'; $detail = "unexpected validator status $($run.status)"
            }
            $Script:MutationResults.Add([pscustomobject]@{ id=$id; status=$mutationStatus; validator_suite_id=$suiteId; target_path=$targetRel; detail=$detail; validator_result=$run })
        }
        catch {
            $Script:MutationResults.Add([pscustomobject]@{ id=$id; status='INFRA_FAILURE'; validator_suite_id=[string]$probe.validator_suite_id; target_path=[string]$probe.target_path; detail=$_.Exception.Message })
        }
        finally {
            if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue }
            if (Test-Path -LiteralPath $workspace) { Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}

function Write-FinalResult {
    param([string]$Path,[string]$Status,[string]$FatalError='')
    $passedTotal = 0
    $failedTotal = 0
    $countedSuiteResults = 0

    foreach ($suiteResult in $Script:SuiteResults) {
        if (
            $suiteResult.counted -eq $true -and
            $null -ne $suiteResult.passed -and
            $null -ne $suiteResult.failed
        ) {
            $passedTotal += [int]$suiteResult.passed
            $failedTotal += [int]$suiteResult.failed
            $countedSuiteResults++
        }
    }

    # Normalize the generic Lists once. ConvertTo-Json should see plain object arrays rather than
    # re-enumerating List[object] values inside nested ordered dictionaries.
    $suiteArray = [object[]]$Script:SuiteResults.ToArray()
    $mutationArray = [object[]]$Script:MutationResults.ToArray()

    $obj = [ordered]@{
        schema_version = 1
        baseline = 'FDM Validation Baseline v1'
        baseline_state = 'CANDIDATE'
        authority_commit = if ($null -ne $Script:Manifest) { [string]$Script:Manifest.authority_commit } else { $null }
        execution_status = $Status
        started_utc = $Script:StartedUtc.ToString('o')
        completed_utc = [DateTime]::UtcNow.ToString('o')
        historical_841_36 = 'EVIDENCE_ONLY'
        source_derived_known_subtotal = 642
        assertion_totals = [ordered]@{ passed=[int]$passedTotal; failed=[int]$failedTotal; counted_suite_results=$countedSuiteResults }
        suites = $suiteArray
        mutation = [ordered]@{ requested=[bool]$RunMutations; policy='NEW_ONLY'; historical_probes_imported=$false; results=$mutationArray }
        fatal_error = $FatalError
        commit = $null
        push = $null
        physics_delta = 'NONE'
    }
    $json = $obj | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($Path, $json + "`n", (New-Object Text.UTF8Encoding($false)))
}

try {
    $Script:RepoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Script:RepoRoot)) { throw 'run from inside the Maverick git checkout' }

    $manifestPath = Join-Path $Script:RepoRoot 'Tools/fdm_validation_baseline_v1.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "manifest missing: $manifestPath" }
    $Script:Manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$Script:Manifest.state -ne 'CANDIDATE') { throw "manifest state must be CANDIDATE" }
    if ([string]$Script:Manifest.authority_commit -ne '460713aeb93ad5345edf02562f9ab20c8c3efe9b') { throw 'unexpected authority commit in manifest' }

    if ([string]::IsNullOrWhiteSpace($ResultsDir)) {
        $ResultsDir = Join-Path ([IO.Path]::GetTempPath()) ("MaverickFdmValidation-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    }
    New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
    $ResultsDir = (Resolve-Path -LiteralPath $ResultsDir).Path

    $Script:InitialStatus = @(Get-GitStatusLines)
    Assert-OnlyImplementationDirty $Script:InitialStatus
    Assert-AuthorityAndInventory

    # Dependencies are resolved before any suite runs: missing Python or Unity is a hard NO-GO.
    $Script:PythonExe = Resolve-PythonExecutable
    $Script:UnityExe = Resolve-UnityExecutable

    $runSurfaces = @($Script:Manifest.surfaces | Where-Object { $_.classification -ne 'EXCLUDED_WITH_REASON' })
    foreach ($surface in $runSurfaces) {
        $result = Invoke-BaselineSurface $surface $Script:RepoRoot $ResultsDir
        $Script:SuiteResults.Add($result)
    }

    $baselineById = @{}
    foreach ($r in $Script:SuiteResults) { $baselineById[[string]$r.id] = $r }
    if ($RunMutations) { Invoke-Mutations $baselineById $ResultsDir }

    $infra = @($Script:SuiteResults | Where-Object { $_.status -eq 'INFRA_FAILURE' })
    $failed = @($Script:SuiteResults | Where-Object { $_.status -eq 'FAIL' })
    $mutInfra = @($Script:MutationResults | Where-Object { $_.status -eq 'INFRA_FAILURE' })
    $mutSurvived = @($Script:MutationResults | Where-Object { $_.status -eq 'SURVIVED' })

    if ($infra.Count -gt 0 -or $mutInfra.Count -gt 0) {
        $Script:OverallStatus = 'INFRA_FAILURE'; $Script:ExitCode = 2
    } elseif ($failed.Count -gt 0 -or $mutSurvived.Count -gt 0) {
        $Script:OverallStatus = 'FAIL'; $Script:ExitCode = 1
    } else {
        $Script:OverallStatus = 'PASS'; $Script:ExitCode = 0
    }
}
catch {
    $Script:OverallStatus = 'INFRA_FAILURE'
    $Script:ExitCode = 2
    $Script:FatalError = $_.Exception.Message
}
finally {
    $postError = ''
    if ($null -ne $Script:RepoRoot -and -not [string]::IsNullOrWhiteSpace($Script:RepoRoot)) {
        try {
            if ($null -ne $Script:Manifest) {
                # Re-hash every authority validation surface and reject any checkout dirty-state delta.
                foreach ($surface in $Script:Manifest.surfaces) {
                    $path = [string]$surface.path
                    $full = Join-Path $Script:RepoRoot ($path -replace '/', [IO.Path]::DirectorySeparatorChar)
                    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "surface disappeared during run: $path" }
                    $blob = (& git -C $Script:RepoRoot hash-object -- $path 2>$null).Trim()
                    if ($LASTEXITCODE -ne 0 -or $blob -ne [string]$surface.git_blob) { throw "surface changed during run: $path" }
                }
                Assert-StatusUnchanged
            }
        } catch {
            $postError = $_.Exception.Message
            $Script:OverallStatus = 'INFRA_FAILURE'; $Script:ExitCode = 2
        }
    }

    if (-not (Get-Variable -Name FatalError -Scope Script -ErrorAction SilentlyContinue)) { $Script:FatalError = '' }
    if (-not [string]::IsNullOrEmpty($postError)) {
        if ([string]::IsNullOrEmpty($Script:FatalError)) { $Script:FatalError = $postError }
        else { $Script:FatalError += "; post-run integrity failure: " + $postError }
    }

    if (-not [string]::IsNullOrWhiteSpace($ResultsDir)) {
        try {
            New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
            $finalPath = Join-Path $ResultsDir 'baseline_v1_result.json'
            Write-FinalResult $finalPath $Script:OverallStatus $Script:FatalError
            Write-Host "FDM_VALIDATION_BASELINE_V1_RESULT=$finalPath"
        } catch {
            Write-Error "could not write final result JSON: $($_.Exception.Message)"
            $Script:ExitCode = 2
        }
    }

    Write-Host "BASELINE V1          CANDIDATE"
    Write-Host "EXECUTION STATUS     $($Script:OverallStatus)"
    Write-Host "COMMIT               NONE"
    Write-Host "PUSH                 NONE"
    Write-Host "PHYSICS DELTA         NONE"
    Write-Host "HISTORICAL 841/36    EVIDENCE ONLY"
}

exit $Script:ExitCode
