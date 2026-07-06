param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [int]$MaxBackups = 5
)

$ErrorActionPreference = "Stop"

$projectRootPath = (Resolve-Path $ProjectRoot).Path
$backupRoot = Join-Path $projectRootPath "ProjectBackups"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $backupRoot "S_G_backup_$timestamp.zip"

$backupItems = @(
    "Assets",
    "Packages",
    "ProjectSettings",
    "UserSettings",
    ".vscode",
    "S_G.slnx",
    "Assembly-CSharp.csproj",
    "Assembly-CSharp-Editor.csproj"
)

$sourcePaths = @()
foreach ($item in $backupItems) {
    $path = Join-Path $projectRootPath $item
    if (Test-Path $path) {
        $sourcePaths += $path
    }
}

if ($sourcePaths.Count -eq 0) {
    throw "No project files were found to back up."
}

Compress-Archive -Path $sourcePaths -DestinationPath $backupPath -CompressionLevel Optimal

$backups = @(Get-ChildItem -Path $backupRoot -Filter "S_G_backup_*.zip" -File | Sort-Object CreationTimeUtc, Name)
$overflowCount = $backups.Count - $MaxBackups
if ($overflowCount -gt 0) {
    $backups | Select-Object -First $overflowCount | Remove-Item -Force
}

Write-Output "Created backup: $backupPath"
