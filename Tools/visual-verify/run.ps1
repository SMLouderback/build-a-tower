<#
.SYNOPSIS
  Build-A-Tower visual verify loop: open sandbox, play, place scenario rooms, screenshot, analyze.

.EXAMPLE
  .\Tools\visual-verify\run.ps1 -Scenario micro-office-adjacency

.EXAMPLE
  .\Tools\visual-verify\run.ps1 -Scenario micro-office-adjacency -SkipPlay
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$Scenario,

  [string]$ProjectRoot = "",
  [switch]$SkipOpenScene,
  [switch]$SkipPlay,
  [switch]$KeepPlaying,
  [int]$SettleMs = 2500,
  [int]$Width = 1600,
  [int]$Height = 900
)

$ErrorActionPreference = "Stop"

if (-not $ProjectRoot) {
  $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$scenarioPath = $Scenario
if (-not [System.IO.Path]::IsPathRooted($scenarioPath)) {
  $candidate = Join-Path $ProjectRoot "Tools\visual-verify\scenarios\$Scenario"
  if (Test-Path $candidate) { $scenarioPath = $candidate }
  elseif (Test-Path ($candidate + ".json")) { $scenarioPath = $candidate + ".json" }
  elseif (Test-Path (Join-Path $ProjectRoot $Scenario)) { $scenarioPath = Join-Path $ProjectRoot $Scenario }
}

if (-not (Test-Path $scenarioPath)) {
  throw "Scenario not found: $Scenario"
}

$scenarioPath = (Resolve-Path $scenarioPath).Path
$label = [System.IO.Path]::GetFileNameWithoutExtension($scenarioPath)
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $ProjectRoot "Temp\visual-verify\$label-$stamp"
# Pipeline capture_game_view sandboxes under Assets/; we copy out afterward.
$captureDir = Join-Path $ProjectRoot "Assets\Temp\visual-verify\latest"
$dropDir = Join-Path $ProjectRoot "Temp\visual-verify"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $captureDir | Out-Null
New-Item -ItemType Directory -Force -Path $dropDir | Out-Null
$shotPath = Join-Path $outDir "game.png"
$captureShot = Join-Path $captureDir "game.png"
# Drop file avoids PowerShell/--args quoting issues with spaces in paths.
$dropScenario = Join-Path $dropDir "current-scenario.json"
Copy-Item -Force $scenarioPath $dropScenario
# Mirror drop into Assets/Temp so Play Mode scripts using relative Temp/ still work if cwd differs.
Copy-Item -Force $scenarioPath (Join-Path $ProjectRoot "Assets\Temp\visual-verify\current-scenario.json")


Write-Host "=== visual-verify ==="
Write-Host "project : $ProjectRoot"
Write-Host "scenario: $scenarioPath"
Write-Host "out     : $outDir"

function Invoke-Unity([string[]]$CmdArgs) {
  Write-Host "> unity $($CmdArgs -join ' ')"
  & unity @CmdArgs
  if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
    throw "unity exit $LASTEXITCODE for: $($CmdArgs -join ' ')"
  }
}

Push-Location $ProjectRoot
try {
  Invoke-Unity @("command", "set_autotick", "--enable", "true")

  if (-not $SkipOpenScene) {
    # Must be edit-mode for open_scene.
    $status = & unity command editor_status 2>&1 | Out-String
    if ($status -match '"playMode"\s*:\s*"playing"') {
      Invoke-Unity @("command", "editor_stop")
      Start-Sleep -Milliseconds 1500
    }
    Invoke-Unity @("command", "open_scene", "--path", "Assets/Scenes/TowerSandbox.unity")
  }

  if (-not $SkipPlay) {
    $status = & unity command editor_status 2>&1 | Out-String
    if ($status -notmatch '"playMode"\s*:\s*"playing"') {
      Invoke-Unity @("command", "editor_play")
      Start-Sleep -Milliseconds 3000
    }
  }

  # Place rooms from drop-file scenario (no --args quoting).
  Invoke-Unity @(
    "command", "run_script",
    "--file", "AgentScripts/VisualVerify.cs",
    "--entry", "VisualVerify.PlaceScenario"
  )

  Start-Sleep -Milliseconds $SettleMs

  # Capture under Assets/Temp (pipeline authoring root), then copy to Temp/.
  Invoke-Unity @(
    "command", "capture_game_view",
    "--width", "$Width",
    "--height", "$Height",
    "--source", "camera",
    "--save_path", "Assets/Temp/visual-verify/latest/game.png"
  )

  Invoke-Unity @(
    "command", "run_script",
    "--file", "AgentScripts/VisualVerify.cs",
    "--entry", "VisualVerify.WriteManifest"
  )

  if (Test-Path $captureShot) {
    Copy-Item -Force $captureShot $shotPath
  }
  $maniCandidates = @(
    (Join-Path $captureDir "game.manifest.json"),
    (Join-Path $dropDir "latest\game.manifest.json"),
    (Join-Path $ProjectRoot "Temp\visual-verify\latest\game.manifest.json")
  )
  foreach ($m in $maniCandidates) {
    if (Test-Path $m) {
      Copy-Item -Force $m (Join-Path $outDir "game.manifest.json")
      break
    }
  }

  $analysis = Join-Path $outDir "game.analysis.json"
  python (Join-Path $ProjectRoot "Tools\visual-verify\analyze_screenshot.py") `
    --image $shotPath `
    --scenario $scenarioPath `
    --out $analysis
  $analyzeCode = $LASTEXITCODE

  Write-Host ""
  Write-Host "screenshot: $shotPath"
  Write-Host "analysis  : $analysis"
  if ($analyzeCode -ne 0) {
    Write-Host "RESULT: FAIL (magenta gate)" -ForegroundColor Red
  } else {
    Write-Host "RESULT: PASS (magenta gate) - still Read the PNG for visual QA" -ForegroundColor Green
  }

  if (-not $KeepPlaying) {
    Invoke-Unity @("command", "editor_stop")
  }

  exit $analyzeCode
}
finally {
  Pop-Location
}
