<#
.SYNOPSIS
  游戏版本更新后，把最新原版 data.unity3d 上传/替换为 GitHub Release 附件。
  用于"Release 附件模型"：原版包不再提交进 git，由构建工作流从最新附件拉取。

.EXAMPLE
  # 步骤：先登录 gh
  gh auth login

  # 游戏更新后，把新提取的原版包上传（同名附件会被覆盖，Release 上永远只有最新一份）
  .\scripts\upload-data-unity3d.ps1 -BundlePath "C:\Downloads\data.unity3d"
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$BundlePath,

  [string]$Repo = "UnrealMultiple/TerrariaSinicization",
  [string]$Tag  = "data-unity3d",
  [string]$Title = "data.unity3d（原版资源）",
  [string]$Notes = "游戏更新后的最新原版资源包，由 scripts/upload-data-unity3d.ps1 上传。"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BundlePath)) { throw "找不到文件: $BundlePath" }

# 0) 检查 gh 可用且已登录
gh --version *> $null
if ($LASTEXITCODE -ne 0) { throw "未安装 gh CLI，请先安装 https://cli.github.com/" }
gh auth status *> $null
if ($LASTEXITCODE -ne 0) { throw "gh 未登录，请先执行 gh auth login" }

# 1) 确保固定 Release 存在（不存在则创建）
gh release view $Tag --repo $Repo *> $null
if ($LASTEXITCODE -ne 0) {
  Write-Host "Release '$Tag' 不存在，创建..."
  gh release create $Tag --repo $Repo --title $Title --notes $Notes
  if ($LASTEXITCODE -ne 0) { throw "创建 Release 失败" }
}

# 2) 上传并覆盖同名附件（--clobber），保证该 Release 上永远只有最新一份
Write-Host "上传 $BundlePath -> $Repo Release '$Tag' ..."
gh release upload $Tag $BundlePath --repo $Repo --clobber
if ($LASTEXITCODE -ne 0) { throw "上传失败" }

Write-Host "完成。构建工作流会从该附件拉取最新原版包作为构建输入。"
Write-Host "（可选）每月清理工作流会自动删除历史遗留的多余同名附件。"