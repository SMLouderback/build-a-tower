# Installs all *_gen.png files from Cursor assets folder into Resources/Art/Agents.
$genDir = "C:\Users\smlou\.cursor\projects\c-OldPC-Importaint-Docs-Work-Steve-Escape\assets"
$install = Join-Path $PSScriptRoot "Install-AgentWalkSheet.ps1"
$outDir = (Resolve-Path (Join-Path $PSScriptRoot "..\Resources\Art\Agents")).Path

Get-ChildItem $genDir -Filter "*_gen.png" | ForEach-Object {
    $key = $_.BaseName -replace '_gen$',''
    & $install -SourcePath $_.FullName -SheetKey $key -OutDir $outDir
}

Write-Host "Done batch install."
