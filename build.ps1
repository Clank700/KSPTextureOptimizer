$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Could not find .NET Framework csc.exe"
}

$outDir = Join-Path $root "bin"
New-Item -ItemType Directory -Force $outDir | Out-Null

$appSources = @(
    (Join-Path $root "src\KSPTextureOptimizer.App\Core.cs"),
    (Join-Path $root "src\KSPTextureOptimizer.App\Program.cs")
)
$appOut = Join-Path $root "KSPTextureOptimizer.exe"

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
    "/out:$appOut" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    $appSources

if ($LASTEXITCODE -ne 0) { throw "App compile failed." }

$testSources = @(
    (Join-Path $root "src\KSPTextureOptimizer.App\Core.cs"),
    (Join-Path $root "tests\KSPTextureOptimizer.Tests\TestRunner.cs")
)
$testOut = Join-Path $outDir "KSPTextureOptimizer.Tests.exe"

& $csc /nologo /target:exe /platform:x64 /optimize+ `
    "/out:$testOut" `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    $testSources

if ($LASTEXITCODE -ne 0) { throw "Test compile failed." }

$toolsDir = Join-Path $root "Tools"
New-Item -ItemType Directory -Force $toolsDir | Out-Null
$toolSource = Join-Path $root "Tools\texconv.exe"
$toolDest = Join-Path $toolsDir "texconv.exe"
if ((Test-Path $toolSource) -and ($toolSource -ne $toolDest)) {
    Copy-Item $toolSource $toolDest -Force
}

Write-Host "Built:"
Write-Host "  $appOut"
Write-Host "  $testOut"
