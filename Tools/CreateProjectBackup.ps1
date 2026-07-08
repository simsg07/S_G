param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [ValidateSet("manual", "before_core_system", "after_core_system")]
    [string]$Milestone = "manual",
    [int]$MaxBackups = 5,
    [switch]$PruneOldBackups
)

$ErrorActionPreference = "Stop"

$projectRootPath = (Resolve-Path $ProjectRoot).Path
$backupRoot = Join-Path $projectRootPath "ProjectBackups"
$latestRoot = Join-Path $backupRoot "Latest"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
New-Item -ItemType Directory -Force -Path $latestRoot | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $backupRoot "S_G_backup_${timestamp}_${Milestone}.zip"

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

$latestBackupPath = Join-Path $latestRoot "S_G_latest_backup.zip"
$latestInfoPath = Join-Path $latestRoot "latest-backup-info.txt"
Copy-Item -LiteralPath $backupPath -Destination $latestBackupPath -Force
@(
    "source=$backupPath",
    "created=$timestamp",
    "milestone=$Milestone"
) | Set-Content -LiteralPath $latestInfoPath -Encoding UTF8

if (($PruneOldBackups -or $MaxBackups -gt 0) -and $MaxBackups -gt 0) {
    $backups = @(Get-ChildItem -Path $backupRoot -Filter "S_G_backup_*.zip" -File | Sort-Object Name)
    $overflowCount = $backups.Count - $MaxBackups
    if ($overflowCount -gt 0) {
        $backups | Select-Object -First $overflowCount | Remove-Item -Force
    }
}

Write-Output "Created backup: $backupPath"
Write-Output "Updated latest backup: $latestBackupPath"
