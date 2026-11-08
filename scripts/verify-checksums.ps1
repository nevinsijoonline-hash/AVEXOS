#!/usr/bin/env pwsh
# Verify SHA256 checksums of built binaries
# Usage: .\scripts\verify-checksums.ps1 [-ChecksumFile <path>]

param(
    [string]$ChecksumFile = "checksums.sha256"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ChecksumFile)) {
    Write-Error "Checksum file not found: $ChecksumFile"
    Write-Host "Run 'scripts\generate-checksums.ps1' first." -ForegroundColor Yellow
    exit 1
}

Write-Host "Verifying checksums from: $ChecksumFile" -ForegroundColor Cyan

$buildDir = Join-Path $PSScriptRoot "..\src\AVEXOS.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64"

if (-not (Test-Path $buildDir)) {
    Write-Error "Build directory not found: $buildDir"
    exit 1
}

$lines = Get-Content -Path $ChecksumFile | Where-Object { $_ -match "^[A-Fa-f0-9]{64}\s+" }
$failed = 0
$passed = 0

foreach ($line in $lines) {
    if ($line -match "^([A-Fa-f0-9]{64})\s+(.+)$") {
        $expectedHash = $Matches[1]
        $fileName = $Matches[2]
        $fullPath = Join-Path $buildDir $fileName

        if (-not (Test-Path $fullPath)) {
            Write-Host "  MISSING: $fileName" -ForegroundColor Red
            $failed++
            continue
        }

        $actualHash = (Get-FileHash -Path $fullPath -Algorithm SHA256).Hash
        
        if ($actualHash -eq $expectedHash) {
            Write-Host "  OK: $fileName" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  FAILED: $fileName" -ForegroundColor Red
            Write-Host "    Expected: $expectedHash" -ForegroundColor Gray
            Write-Host "    Actual:   $actualHash" -ForegroundColor Gray
            $failed++
        }
    }
}

Write-Host "`nVerification complete:" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor Green
Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })

exit $failed
