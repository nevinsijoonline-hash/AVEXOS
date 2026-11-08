#!/usr/bin/env pwsh
# Generate SHA256 checksums for all built binaries
# Usage: .\scripts\generate-checksums.ps1 [-OutputFile <path>]

param(
    [string]$OutputFile = "checksums.sha256"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating checksums for AVEXOS binaries..." -ForegroundColor Cyan

$buildDir = Join-Path $PSScriptRoot "..\src\AVEXOS.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64"

if (-not (Test-Path $buildDir)) {
    Write-Error "Build directory not found: $buildDir"
    Write-Host "Run 'dotnet build src/AVEXOS.App/AVEXOS.App.csproj' first." -ForegroundColor Yellow
    exit 1
}

$files = Get-ChildItem -Path $buildDir -Include "*.exe","*.dll" -Recurse | Where-Object { $_.Name -like "AVEXOS*" -or $_.Name -eq "e_sqlite3.dll" }

$checksums = @()
foreach ($file in $files) {
    $hash = Get-FileHash -Path $file.FullName -Algorithm SHA256
    $checksums += "$($hash.Hash)  $($file.Name)"
    Write-Host "  $($file.Name)" -ForegroundColor Gray
}

$checksums | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "`nChecksums written to: $OutputFile" -ForegroundColor Green
Write-Host "Total files: $($files.Count)" -ForegroundColor Cyan
