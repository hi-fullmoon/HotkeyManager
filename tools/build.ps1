# HotkeyManager 打包脚本：更新版本与 Changelog（可选）→ dotnet publish → ISCC 编译安装包
#
# 用法：
#   ./tools/build.ps1                 # 沿用当前版本号，直接打包
#   ./tools/build.ps1 -Version 1.1.0  # 先同步版本并归档 Changelog，再打包
#
# 产物：installer/HotkeyManagerSetup-<版本>.exe
[CmdletBinding()]
param(
    # 新版本号，格式 x.y.z（允许带 v 前缀）。省略时沿用 csproj 中的当前版本。
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root 'src/HotkeyManager/HotkeyManager.csproj'
$issPath    = Join-Path $root 'installer/setup.iss'

# ---- 1. 版本号 ----
if ($Version) {
    # 版本号写入逻辑统一在 bump-version.ps1 中维护
    & (Join-Path $PSScriptRoot 'bump-version.ps1') $Version
}

$csproj = [IO.File]::ReadAllText($csprojPath)
if ($csproj -notmatch '<Version>([^<]+)</Version>') {
    throw "未在 $csprojPath 中找到 <Version> 节点"
}
$Version = $Matches[1]
Write-Host "打包版本：$Version"

# ---- 2. 发布单文件 exe ----
Write-Host "`n==> dotnet publish"
dotnet publish (Join-Path $root 'src/HotkeyManager') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（退出码 $LASTEXITCODE）" }

# ---- 3. 编译安装包 ----
$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "未找到 Inno Setup 6 的 ISCC.exe，请先安装：https://jrsoftware.org/isinfo.php" }

Write-Host "`n==> ISCC 编译安装包"
& $iscc $issPath
if ($LASTEXITCODE -ne 0) { throw "ISCC 编译失败（退出码 $LASTEXITCODE）" }

$output = Join-Path $root "installer/HotkeyManagerSetup-$Version.exe"
if (Test-Path $output) {
    Write-Host "`n打包完成：$output"
} else {
    throw "ISCC 报告成功，但未找到预期产物 $output"
}
