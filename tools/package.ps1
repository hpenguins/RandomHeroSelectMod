<#
.SYNOPSIS
    构建并打包 RandomHeroSelectMod。

.DESCRIPTION
    产出 staging\RandomHeroSelectMod\（dll + sidecar + config.json）与
    仓库根目录下的 RandomHeroSelectMod.zip，前者可直接复制进
    游戏目录的 AstralParty_ModLoader\mods\。

.PARAMETER Deploy
    给出游戏目录时，构建完直接把模组部署进去（已存在则覆盖 dll 与 sidecar，
    保留玩家自己的 config.json）。目录不存在时自动按默认 Steam 路径解析。

.PARAMETER Version
    版本号。省略时取 sidecar 里的 version。
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [string] $Version,
    [switch] $NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoRoot 'source'
$stageDir = Join-Path $repoRoot 'staging\RandomHeroSelectMod'
$sidecar = Join-Path $repoRoot 'RandomHeroSelectMod.json'

if (-not $Version) {
    $Version = (Get-Content -LiteralPath $sidecar -Raw | ConvertFrom-Json).version
}
Write-Host "== 版本 $Version"

Write-Host '== 编译'
Push-Location $sourceDir
try {
    dotnet build RandomHeroSelectMod.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "编译失败（退出码 $LASTEXITCODE）" }
}
finally { Pop-Location }

$outDir = Join-Path $sourceDir 'bin\Release\netstandard2.0'
$builtDll = Join-Path $outDir 'RandomHeroSelectMod.dll'
if (-not (Test-Path -LiteralPath $builtDll)) { throw "缺少构建产物: $builtDll" }

Write-Host '== 打包'
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Copy-Item -LiteralPath $builtDll -Destination $stageDir
Copy-Item -LiteralPath $sidecar -Destination (Join-Path $stageDir 'RandomHeroSelectMod.json')
Copy-Item -LiteralPath (Join-Path $repoRoot 'config.json') -Destination (Join-Path $stageDir 'config.json')
Get-ChildItem -LiteralPath $stageDir | ForEach-Object { Write-Host "  $($_.Name)  $($_.Length) B" }

if (-not $NoZip) {
    $zip = Join-Path $repoRoot "RandomHeroSelectMod-$Version.zip"
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path $stageDir -DestinationPath $zip -CompressionLevel Optimal
    Copy-Item -LiteralPath $zip -Destination (Join-Path $repoRoot 'RandomHeroSelectMod.zip') -Force
    Write-Host "== 已生成 $zip"
    Write-Host "== 已生成 RandomHeroSelectMod.zip（固定下载地址用）"
}

if ($GameDir) {
    Write-Host "== 部署到 $GameDir"
    New-Item -ItemType Directory -Force -Path $GameDir | Out-Null

    $dllTarget = Join-Path $GameDir 'RandomHeroSelectMod.dll'
    $jsonTarget = Join-Path $GameDir 'RandomHeroSelectMod.json'
    $configTarget = Join-Path $GameDir 'config.json'
    $configBackup = $null

    # 保留玩家自己的配置：部署时不覆盖已存在的 config.json。
    if (Test-Path -LiteralPath $configTarget) {
        $configBackup = "$configTarget.deploybak-$((Get-Date).ToString('yyyyMMdd-HHmmss'))"
        Move-Item -LiteralPath $configTarget -Destination $configBackup -Force
        Write-Host "  已有配置已备份为 $([IO.Path]::GetFileName($configBackup))"
    }

    Copy-Item -LiteralPath $builtDll -Destination $dllTarget -Force
    Copy-Item -LiteralPath $sidecar -Destination $jsonTarget -Force
    if ($configBackup) {
        Move-Item -LiteralPath $configBackup -Destination $configTarget -Force
    }
    else {
        Copy-Item -LiteralPath (Join-Path $repoRoot 'config.json') -Destination $configTarget
    }

    Write-Host '  已部署:'
    Get-ChildItem -LiteralPath $GameDir | ForEach-Object { Write-Host "    $($_.Name)  $($_.Length) B" }
}

Write-Host '== 完成'
