# HotkeyManager 版本号更新：同步写入 src/HotkeyManager/HotkeyManager.csproj 和 installer/setup.iss
#
# 用法：
#   ./tools/bump-version.ps1 patch    # 0.1.0 -> 0.1.1
#   ./tools/bump-version.ps1 minor    # 0.1.0 -> 0.2.0
#   ./tools/bump-version.ps1 major    # 0.1.0 -> 1.0.0
#   ./tools/bump-version.ps1 1.2.3    # 直接指定版本号（允许带 v 前缀）
[CmdletBinding()]
param(
    # major / minor / patch，或显式版本号 x.y.z
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root 'src/HotkeyManager/HotkeyManager.csproj'
$issPath    = Join-Path $root 'installer/setup.iss'
$utf8NoBom  = New-Object System.Text.UTF8Encoding($false)

$csproj = [IO.File]::ReadAllText($csprojPath)
if ($csproj -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "未在 $csprojPath 中找到 x.y.z 格式的 <Version> 节点"
}
$currentVersion = $Matches[0] -replace '</?Version>', ''
$major, $minor, $patch = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]

$Target = $Target.Trim().TrimStart('v', 'V')
switch -Regex ($Target) {
    '^\d+\.\d+\.\d+$' { $newVersion = $Target }
    '^major$'         { $newVersion = "$($major + 1).0.0" }
    '^minor$'         { $newVersion = "$major.$($minor + 1).0" }
    '^patch$'         { $newVersion = "$major.$minor.$($patch + 1)" }
    default           { throw "无效的参数：'$Target'（应为 major / minor / patch 或 x.y.z 版本号）" }
}

if ($newVersion -eq $currentVersion) {
    Write-Host "版本号已是 $newVersion，无需修改"
    return # 不用 exit：被 build.ps1 以 & 调用时 exit 会连整个打包流程一起终止
}

$csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
[IO.File]::WriteAllText($csprojPath, $csproj, $utf8NoBom)

$iss = [IO.File]::ReadAllText($issPath)
if ($iss -notmatch '(?m)^#define AppVersion "[^"]+"') {
    throw "未在 $issPath 中找到 #define AppVersion"
}
$iss = $iss -replace '(?m)^#define AppVersion "[^"]+"', "#define AppVersion `"$newVersion`""
[IO.File]::WriteAllText($issPath, $iss, $utf8NoBom)

Write-Host "版本号：$currentVersion -> $newVersion"
