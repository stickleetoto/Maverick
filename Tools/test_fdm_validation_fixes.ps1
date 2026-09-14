param(
    [string]$UnityPath = 'D:\unitys\6000.3.16f1\Editor\Unity.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $RepoRoot

function Read-Text([string]$RelativePath) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing file: $RelativePath"
    }
    return [IO.File]::ReadAllText($path)
}

function Write-Text([string]$RelativePath, [string]$Text) {
    $path = Join-Path $RepoRoot $RelativePath
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
}

function Count-Exact([string]$Text, [string]$Needle) {
    return ([regex]::Matches($Text, [regex]::Escape($Needle))).Count
}

function Detect-Newline([string]$Text) {
    if ($Text.Contains("`r`n")) { return "`r`n" }
    return "`n"
}

function Assert-Count([string]$Text, [string]$Needle, [int]$Expected, [string]$Label) {
    $count = Count-Exact $Text $Needle
    if ($count -ne $Expected) {
        throw "$Label: expected $Expected occurrence(s), found $count"
    }
}

Write-Host '=== Maverick FDM validation fix driver ===' -ForegroundColor Cyan

$branch = (& git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) { throw 'git branch --show-current failed' }
if ($branch -ne 'sol/fdm-validation-fixes') {
    throw "Run this only on sol/fdm-validation-fixes (current: $branch)"
}

$changed = New-Object System.Collections.Generic.List[string]

# -----------------------------------------------------------------------------
# 1. Phase 2 synthetic snapshots: current readiness requires angular dynamics.
# -----------------------------------------------------------------------------
$rel = 'Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsPhase2Validation.cs'
$s = Read-Text $rel
$old = '            snapshot.propulsionAcceptableForLiveFlight = true;'
$marker = '            snapshot.angularDynamicsAcceptable = true;'
$nl = Detect-Newline $s

$markerCount = Count-Exact $s $marker
if ($markerCount -eq 0) {
    Assert-Count $s $old 2 'Phase2 propulsion-ready fixture sites'
    $s = $s.Replace($old, $old + $nl + $marker)
    Write-Text $rel $s
    $changed.Add($rel)
}
elif ($markerCount -ne 2) {
    throw "Phase2 angular readiness marker count is $markerCount; expected 0 or 2"
}

# -----------------------------------------------------------------------------
# 2/3. Freeze hardening + integration rigs: explicitly enable the production
# gyroscopic compensation path before asking for OPERATIONALLY_LIVE_READY.
# -----------------------------------------------------------------------------
foreach ($rel in @(
    'Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsFreezeHardeningValidation.cs',
    'Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsIntegrationValidation.cs'
)) {
    $s = Read-Text $rel
    $nl = Detect-Newline $s
    $old = '            body.acceptNonAuthoritativePropulsion = true;'
    $marker = '            body.applyBackendGyroscopicCompensation = true;'

    $markerCount = Count-Exact $s $marker
    if ($markerCount -eq 0) {
        Assert-Count $s $old 1 "$rel propulsion-acceptance fixture site"
        $s = $s.Replace($old, $old + $nl + $marker)
        Write-Text $rel $s
        $changed.Add($rel)
    }
    elseif ($markerCount -ne 1) {
        throw "$rel gyroscopic readiness marker count is $markerCount; expected 0 or 1"
    }
}

# -----------------------------------------------------------------------------
# 4. Ownership scanner: remove only the known false positives while retaining
# the production writer boundary.
# -----------------------------------------------------------------------------
$rel = 'Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsOwnershipScan.cs'
$s = Read-Text $rel
$nl = Detect-Newline $s

$validationExemption = '            "MavF16ReferenceFlightScenarios.cs"'
if (-not $s.Contains($validationExemption)) {
    $old = '            "MavFlightDynamicsIntegrationValidation.cs"'
    Assert-Count $s $old 1 'Ownership exemption anchor'
    $s = $s.Replace($old, '            "MavFlightDynamicsIntegrationValidation.cs",' + $nl + $validationExemption)
}

$oldMatcher = '                if (code.Contains(ForbiddenTokens[i]))'
$newMatcher = '                if (ContainsForbiddenToken(code, ForbiddenTokens[i]))'
if (-not $s.Contains($newMatcher)) {
    Assert-Count $s $oldMatcher 1 'Ownership matcher'
    $s = $s.Replace($oldMatcher, $newMatcher)
}

$helperMarker = '        private static bool ContainsForbiddenToken(string code, string token)'
if (-not $s.Contains($helperMarker)) {
    $anchor = '        public static string StripCommentsAndStringLiterals(string sourceLine)'
    Assert-Count $s $anchor 1 'Ownership helper insertion point'

    $helpers = @'
        private static bool ContainsForbiddenToken(string code, string token)
        {
            int searchFrom = 0;

            while (searchFrom < code.Length)
            {
                int index = code.IndexOf(token, searchFrom, System.StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int after = index + token.Length;
                bool assignmentToken = token.EndsWith(" =", System.StringComparison.Ordinal);

                // Assignment tokens must not classify equality comparisons ("==") as writes.
                if (!assignmentToken || after >= code.Length || code[after] != '=')
                    return true;

                searchFrom = after + 1;
            }

            return false;
        }

        private static bool IsAllowedHandoverStateTransfer(string fileName, string sourceLine)
        {
            if (!string.Equals(
                    fileName,
                    "MavRuntimeHandoverTarget.cs",
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            string code = StripCommentsAndStringLiterals(sourceLine).Trim();

            // The handover target owns only atomic capture/rollback of motion state.
            // Keep this list exact so a future force/torque writer in the same file still fails.
            return code == "c.linearVelocity = rb.linearVelocity;"
                || code == "c.angularVelocity = rb.angularVelocity;"
                || code == "rb.linearVelocity = linearVelocity;"
                || code == "rb.angularVelocity = angularVelocity;";
        }

'@
    $helpers = $helpers -replace "`r?`n", $nl
    $s = $s.Replace($anchor, $helpers + $anchor)
}

$oldScan = '                    if (IsOwnershipViolation(lines[i]))'
$newScan = '                    if (IsOwnershipViolation(lines[i]) && !IsAllowedHandoverStateTransfer(fileName, lines[i]))'
if (-not $s.Contains($newScan)) {
    Assert-Count $s $oldScan 1 'Ownership scan call site'
    $s = $s.Replace($oldScan, $newScan)
}

Write-Text $rel $s
if (-not $changed.Contains($rel)) { $changed.Add($rel) }

# -----------------------------------------------------------------------------
# 5. WT feel: keep the no-aircraft reason after the handling-only fallback.
# -----------------------------------------------------------------------------
$rel = 'Assets/MaverickFresh/Scripts/MavWTFeelPolishController.cs'
$s = Read-Text $rel
$nl = Detect-Newline $s

$field = '        private bool loggedMissingAircraftAuthority;'
$newField = '        private bool skippedAircraftAwareForMissingAuthority;'
if (-not $s.Contains($newField)) {
    Assert-Count $s $field 1 'WT diagnostic field anchor'
    $s = $s.Replace($field, $field + $nl + $newField)
}

$resetAnchor = '            preset = p;'
$reset = '            skippedAircraftAwareForMissingAuthority = false;'
if (-not $s.Contains($reset)) {
    Assert-Count $s $resetAnchor 1 'WT ApplyPreset reset anchor'
    $s = $s.Replace($resetAnchor, $resetAnchor + $nl + $reset)
}

$reasonAnchor = '                lastApplied = "no aircraft applied yet: WT feel skipped its aircraft-aware pass "'
$reasonFlag = '                skippedAircraftAwareForMissingAuthority = true;'
if (-not $s.Contains($reasonFlag)) {
    Assert-Count $s $reasonAnchor 1 'WT missing-authority reason anchor'
    $s = $s.Replace($reasonAnchor, $reasonFlag + $nl + $reasonAnchor)
}

$finalOld = '            lastApplied = p.ToString();'
$finalNew = '            if (!skippedAircraftAwareForMissingAuthority)' + $nl + '                lastApplied = p.ToString();'
if (-not $s.Contains('if (!skippedAircraftAwareForMissingAuthority)')) {
    Assert-Count $s $finalOld 1 'WT final lastApplied assignment'
    $s = $s.Replace($finalOld, $finalNew)
}

Write-Text $rel $s
if (-not $changed.Contains($rel)) { $changed.Add($rel) }

Write-Host ''
Write-Host 'Patched files:' -ForegroundColor Green
$changed | ForEach-Object { Write-Host "  $_" }

& git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed' }

$expected = @(
    'Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsPhase2Validation.cs',
    'Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsFreezeHardeningValidation.cs',
    'Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsIntegrationValidation.cs',
    'Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsOwnershipScan.cs',
    'Assets/MaverickFresh/Scripts/MavWTFeelPolishController.cs'
)

$diffFiles = @(& git diff --name-only -- $expected)
if ($LASTEXITCODE -ne 0) { throw 'git diff --name-only failed' }

if ($diffFiles.Count -gt 0) {
    & git add -- $expected
    if ($LASTEXITCODE -ne 0) { throw 'git add failed' }

    & git commit -m 'fix: align FDM validators with current readiness contract' -- $expected
    if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }
}
else {
    Write-Host 'Fix commit is already present; no source changes needed.' -ForegroundColor DarkGray
}

$runner = Join-Path $RepoRoot 'Tools/run_fdm_validation.ps1'
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw 'Tools/run_fdm_validation.ps1 is missing. Keep the local baseline tooling files before switching branches.'
}

Write-Host ''
Write-Host '=== Running full FDM validation ===' -ForegroundColor Cyan
& $runner -UnityPath $UnityPath
exit $LASTEXITCODE
