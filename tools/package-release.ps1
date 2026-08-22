# 构建可发布的 Windows 安装包、便携 ZIP 与 SHA-256 校验文件。
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root 'src/HotkeyManager/HotkeyManager.csproj'
$distDir    = if ($env:DIST_DIR) { $env:DIST_DIR } else { Join-Path $root 'dist' }
$utf8NoBom  = New-Object System.Text.UTF8Encoding($false)

$csproj = [IO.File]::ReadAllText($csprojPath)
if ($csproj -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') {
    throw "未在 $csprojPath 中找到 X.Y.Z 格式的 <Version> 节点"
}
$projectVersion = $Matches[1]
$version = if ($env:VERSION) { $env:VERSION } else { $projectVersion }
$artifactVersion = if ($env:ARTIFACT_VERSION) { $env:ARTIFACT_VERSION } else { $version }

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION 必须是 X.Y.Z 格式：$version"
}
if ($version -ne $projectVersion) {
    throw "VERSION $version 与项目版本 $projectVersion 不一致"
}
if ($artifactVersion -notmatch '^[0-9A-Za-z][0-9A-Za-z._+-]*$') {
    throw "ARTIFACT_VERSION 包含不安全的文件名字符：$artifactVersion"
}

& (Join-Path $PSScriptRoot 'build.ps1')

$publishedExe = Join-Path $root 'src/HotkeyManager/bin/Release/net8.0-windows/win-x64/publish/HotkeyManager.exe'
$builtInstaller = Join-Path $root "installer/HotkeyManagerSetup-$version.exe"
if (-not (Test-Path $publishedExe -PathType Leaf)) {
    throw "未找到发布程序：$publishedExe"
}
if (-not (Test-Path $builtInstaller -PathType Leaf)) {
    throw "未找到安装包：$builtInstaller"
}

[IO.Directory]::CreateDirectory($distDir) | Out-Null
$prefix = "HotkeyManager-$artifactVersion-Windows-x64"
$installerPath = Join-Path $distDir "$prefix-Setup.exe"
$portablePath = Join-Path $distDir "$prefix-portable.zip"

foreach ($path in @($installerPath, $portablePath, "$installerPath.sha256", "$portablePath.sha256")) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

Copy-Item -LiteralPath $builtInstaller -Destination $installerPath
Compress-Archive -LiteralPath $publishedExe -DestinationPath $portablePath -CompressionLevel Optimal

foreach ($asset in @($installerPath, $portablePath)) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    $name = Split-Path -Leaf $asset
    [IO.File]::WriteAllText("$asset.sha256", "$hash  $name`n", $utf8NoBom)
}

Write-Host "发布产物：$installerPath"
Write-Host "发布产物：$portablePath"
Write-Host 'SHA-256 校验文件已生成'
