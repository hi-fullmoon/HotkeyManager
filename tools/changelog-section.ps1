# 输出 CHANGELOG.md 中指定版本的 Markdown 内容，供 GitHub Release 使用。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$changelogPath = if ($env:CHANGELOG_FILE) { $env:CHANGELOG_FILE } else { Join-Path $root 'CHANGELOG.md' }
if (-not (Test-Path $changelogPath -PathType Leaf)) {
    throw "找不到 Changelog：$changelogPath"
}

$changelog = [IO.File]::ReadAllText($changelogPath)
$escapedVersion = [regex]::Escape($Version)
$pattern = "(?ms)^## \[$escapedVersion\](?: - \d{4}-\d{2}-\d{2})?[ \t]*\r?\n(?<body>.*?)(?=^## \[|\z)"
$section = [regex]::Match($changelog, $pattern)
if (-not $section.Success) {
    throw "Changelog 中找不到版本 $Version"
}

$body = $section.Groups['body'].Value.Trim()
if ([string]::IsNullOrWhiteSpace($body)) {
    throw "Changelog 中的版本内容为空：$Version"
}

[Console]::Out.WriteLine($body)
