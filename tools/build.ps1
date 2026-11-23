# 自动检测分辨率 v1.2.0 构建脚本
# 用法: powershell -ExecutionPolicy Bypass -File .\tools\build.ps1 [-OutDir <目录>]

param([string]$OutDir = "")

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$Csc  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Ver  = "1.2.0"
if (-not $OutDir) { $OutDir = Join-Path $Root "dist" }

if (-not (Test-Path $Csc)) { throw "找不到 csc.exe: $Csc" }
$Work = Join-Path $env:TEMP ("dra_build_" + [guid]::NewGuid().ToString("N"))
$Payload = Join-Path $Work "payload"
New-Item -ItemType Directory -Force -Path $Payload, $OutDir | Out-Null

function Invoke-Csc([string[]]$Arguments) {
    & $Csc @Arguments
    if ($LASTEXITCODE -ne 0) { throw "csc 失败，退出码 $LASTEXITCODE" }
}

Write-Host "[1/4] 编译主程序"
Invoke-Csc @(
    "/nologo", "/target:winexe", "/optimize+", "/codepage:65001",
    "/define:APP",
    "/out:$Payload\app.exe",
    "/win32icon:$Root\res\app.ico",
    "/win32manifest:$Root\res\app.manifest",
    "/resource:$Root\res\app.ico,app.ico",
    "/resource:$Root\res\mis_r_s.ttf,mis_r.ttf",
    "/resource:$Root\res\mis_m_s.ttf,mis_m.ttf",
    "/resource:$Root\res\bg_theme.jpg,bg_theme.jpg",
    "$Root\src\VersionInfo.cs",
    "$Root\src\DeltaResAuto.cs"
)

Write-Host "[2/4] 编译卸载器"
Invoke-Csc @(
    "/nologo", "/target:winexe", "/optimize+", "/codepage:65001",
    "/define:UNINSTALLER",
    "/out:$Payload\uninstall.exe",
    "/win32icon:$Root\res\app.ico",
    "$Root\src\VersionInfo.cs",
    "$Root\src\Uninstaller.cs"
)

Write-Host "[3/4] 生成 payload 与 SHA256 清单"
Copy-Item "$Root\README-installed.txt" "$Payload\README.txt" -Force
Copy-Item "$Root\LICENSE.txt" "$Payload\LICENSE.txt" -Force
Copy-Item "$Root\settings.example.ini" "$Payload\settings.example.ini" -Force

$files = @("app.exe", "uninstall.exe", "README.txt", "LICENSE.txt", "settings.example.ini")
$lines = foreach ($n in $files) {
    $h = (Get-FileHash (Join-Path $Payload $n) -Algorithm SHA256).Hash.ToLower()
    "$n|$h"
}
[System.IO.File]::WriteAllLines("$Payload\manifest.txt", $lines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "[4/4] 编译安装器"
$SetupName = "DeltaResAuto_v" + $Ver + "_setup.exe"
$PortableName = "DeltaResAuto_v" + $Ver + "_portable.exe"
$Setup = Join-Path $OutDir $SetupName
Invoke-Csc @(
    "/nologo", "/target:winexe", "/optimize+", "/codepage:65001",
    "/define:INSTALLER",
    "/out:$Setup",
    "/win32icon:$Root\res\app.ico",
    "/resource:$Payload\app.exe,app.exe",
    "/resource:$Payload\uninstall.exe,uninstall.exe",
    "/resource:$Payload\README.txt,README.txt",
    "/resource:$Payload\LICENSE.txt,LICENSE.txt",
    "/resource:$Payload\settings.example.ini,settings.example.ini",
    "/resource:$Payload\manifest.txt,manifest.txt",
    "/resource:$Root\res\app.ico,app.ico",
    "$Root\src\VersionInfo.cs",
    "$Root\src\Installer.cs"
)

$SetupHash = (Get-FileHash $Setup -Algorithm SHA256).Hash.ToLower()
$Portable = Join-Path $OutDir $PortableName
Copy-Item "$Payload\app.exe" $Portable -Force
$PortableHash = (Get-FileHash $Portable -Algorithm SHA256).Hash.ToLower()

$sumLines = @(
    "$SetupHash *$SetupName",
    "$PortableHash *$PortableName"
)
[System.IO.File]::WriteAllLines((Join-Path $OutDir "SHA256SUMS.txt"), $sumLines, (New-Object System.Text.UTF8Encoding($false)))

Remove-Item $Work -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "完成:"
Write-Host "  $Setup"
Write-Host "  $Portable"
Write-Host "  SHA256 $SetupHash"
Write-Host "  SHA256 $PortableHash"
