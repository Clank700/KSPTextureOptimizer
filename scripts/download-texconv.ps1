param(
    [string]$Destination = "$PSScriptRoot\..\Tools"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force $Destination | Out-Null

Write-Host "This project uses Microsoft DirectXTex texconv.exe for DDS conversion."
Write-Host "Preferred install method:"
Write-Host "  winget install Microsoft.DirectXTex.Texconv"
Write-Host ""
Write-Host "If you download texconv manually, place texconv.exe here:"
Write-Host "  $((Resolve-Path $Destination).Path)\texconv.exe"
