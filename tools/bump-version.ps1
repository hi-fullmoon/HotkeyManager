# HotkeyManager 版本号更新：同步 csproj、Inno Setup 与 CHANGELOG.md。
[CmdletBinding()]
param(
    # major / minor / patch，或显式版本号 X.Y.Z（允许带 v 前缀）。
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target,

    # Changelog 发布日期，默认为今天。
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),

    # 仅同步版本文件，不归档 Changelog。
    [switch]$SkipChangelog,

    # 只显示将要执行的修改。
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$root          = Split-Path -Parent $PSScriptRoot
$csprojPath    = Join-Path $root 'src/HotkeyManager/HotkeyManager.csproj'
$issPath       = Join-Path $root 'installer/setup.iss'
$changelogPath = Join-Path $root 'CHANGELOG.md'
$utf8NoBom     = New-Object System.Text.UTF8Encoding($false)

$csproj = [IO.File]::ReadAllText($csprojPath)
if ($csproj -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "未在 $csprojPath 中找到 X.Y.Z 格式的 <Version> 节点"
}
$currentVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
$major, $minor, $patch = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]

$iss = [IO.File]::ReadAllText($issPath)
if ($iss -notmatch '(?m)^#define AppVersion "([^"]+)"') {
    throw "未在 $issPath 中找到 #define AppVersion"
}
if ($Matches[1] -ne $currentVersion) {
    throw "版本不一致：csproj=$currentVersion，setup.iss=$($Matches[1])"
}

$normalizedTarget = $Target.Trim().TrimStart('v', 'V')
switch -Regex ($normalizedTarget) {
    '^major$'             { $newVersion = "$($major + 1).0.0" }
    '^minor$'             { $newVersion = "$major.$($minor + 1).0" }
    '^patch$'             { $newVersion = "$major.$minor.$($patch + 1)" }
    '^\d+\.\d+\.\d+$' { $newVersion = $normalizedTarget }
    default               { throw "无效参数：'$Target'（应为 major / minor / patch 或 X.Y.Z）" }
}

if ($newVersion -eq $currentVersion) {
    throw "新版本与当前版本相同：$currentVersion"
}

$updatedChangelog = $null
if (-not $SkipChangelog) {
    if (-not (Test-Path $changelogPath -PathType Leaf)) {
        throw "找不到 Changelog：$changelogPath"
    }

    $changelog = [IO.File]::ReadAllText($changelogPath)
    $unreleasedPattern = '(?ms)^## \[Unreleased\][ \t]*\r?\n(?<body>.*?)(?=^## \[|\z)'
    $unreleased = [regex]::Match($changelog, $unreleasedPattern)
    if (-not $unreleased.Success) {
        throw 'CHANGELOG.md 缺少 ## [Unreleased] 段落'
    }
    if ([string]::IsNullOrWhiteSpace($unreleased.Groups['body'].Value)) {
        throw 'CHANGELOG.md 的 [Unreleased] 段落为空；请先记录变更或使用 -SkipChangelog'
    }
    if ($changelog -match "(?m)^## \[$([regex]::Escape($newVersion))\](?: |$)") {
        throw "CHANGELOG.md 已存在 $newVersion 版本"
    }

    $lineEnding = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $replacement = "## [Unreleased]$lineEnding$lineEnding## [$newVersion] - $Date"
    $updatedChangelog = [regex]::Replace(
        $changelog,
        '(?m)^## \[Unreleased\][ \t]*$',
        $replacement,
        1)
}

$updatedCsproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
$updatedIss = $iss -replace '(?m)^#define AppVersion "[^"]+"', "#define AppVersion `"$newVersion`""

Write-Host "版本：$currentVersion -> $newVersion"
if (-not $SkipChangelog) {
    Write-Host "Changelog：[Unreleased] -> [$newVersion] - $Date"
}

if ($DryRun) {
    Write-Host 'Dry run：未修改文件'
    return
}

[IO.File]::WriteAllText($csprojPath, $updatedCsproj, $utf8NoBom)
[IO.File]::WriteAllText($issPath, $updatedIss, $utf8NoBom)
if (-not $SkipChangelog) {
    [IO.File]::WriteAllText($changelogPath, $updatedChangelog, $utf8NoBom)
}

Write-Host "已更新：$csprojPath"
Write-Host "已更新：$issPath"
if (-not $SkipChangelog) {
    Write-Host "已更新：$changelogPath"
}
Write-Host "发布标签：v$newVersion"
