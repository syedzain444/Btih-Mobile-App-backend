# Grant IIS write permission for promotion image uploads.
# Run in elevated PowerShell (Run as Administrator) on the API server.
#
# Usage:
#   .\SET_PROMOTION_UPLOADS_PERMISSIONS.ps1
#   .\SET_PROMOTION_UPLOADS_PERMISSIONS.ps1 -ApiRoot "D:\HMIS Mobile App Publish"
#   .\SET_PROMOTION_UPLOADS_PERMISSIONS.ps1 -ApiRoot "D:\path\to\api" -AppPoolName "YourAppPoolName"

param(
    [string]$ApiRoot = "D:\HMIS Mobile App Publish",
    [string]$AppPoolName = "DefaultAppPool"
)

$ErrorActionPreference = "Stop"

$uploads = Join-Path $ApiRoot "wwwroot\uploads\promotions"
if (-not (Test-Path (Join-Path $ApiRoot "HospitalMobileAPPApi.dll")) -and -not (Test-Path (Join-Path $ApiRoot "wwwroot"))) {
    Write-Warning "ApiRoot does not look like the API publish folder: $ApiRoot"
}

New-Item -ItemType Directory -Force -Path $uploads | Out-Null
Write-Host "Folder: $uploads"

$grants = @(
    "IIS_IUSRS",
    "IIS AppPool\$AppPoolName",
    "NETWORK SERVICE"
)

foreach ($id in $grants) {
    Write-Host "Granting Modify to $id ..."
    & icacls $uploads /grant "${id}:(OI)(CI)M" /T | Out-Null
}

Write-Host ""
Write-Host "Current ACL:"
& icacls $uploads

# Quick write probe
$probe = Join-Path $uploads ".iis_write_probe"
try {
    Set-Content -Path $probe -Value "ok" -Force
    Remove-Item $probe -Force
    Write-Host ""
    Write-Host "Write test: PASS"
} catch {
    Write-Host ""
    Write-Host "Write test: FAIL — $($_.Exception.Message)"
    exit 1
}

Write-Host ""
Write-Host "Also in IIS Manager (GUI):"
Write-Host "  1. Open the API site physical path → wwwroot\uploads\promotions"
Write-Host "  2. Properties → Security → Edit → Add"
Write-Host "  3. Add: IIS_IUSRS  and  IIS AppPool\$AppPoolName"
Write-Host "  4. Allow: Modify (includes Write)"
Write-Host "  5. Recycle the app pool"
