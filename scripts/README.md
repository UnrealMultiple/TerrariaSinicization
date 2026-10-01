# 维护脚本与说明（scripts/）

本目录存放仓库维护工具与维护手册。

## upload-data-unity3d.ps1 —— 上传最新原版包

**用途**：游戏版本更新后，把新提取的**原版 `data.unity3d`** 上传为 GitHub Release 附件（固定 tag `data-unity3d`）。构建工作流会从该附件拉取最新原版包作为构建输入，因此**原版包不再提交进 git，Git LFS 配额不会随版本增长**。

**用法**：

```powershell
gh auth login                                            # 首次需要
.\scripts\upload-data-unity3d.ps1 -BundlePath "C:\新提取的\data.unity3d"
```

可选参数：`-Repo`（默认 `UnrealMultiple/TerrariaSinicization`）、`-Tag`（默认 `data-unity3d`）。

**行为**：同名附件会被 `--clobber` 覆盖，该 Release 上永远只有最新一份；Release 不存在时自动创建。

## “只保留最新版”机制

1. 原版包以 Release 附件形式存在（**不计入 Git LFS 配额**，也不占克隆体积）；
2. [cleanup-data-unity3d-releases.yml](../.github/workflows/cleanup-data-unity3d-releases.yml) 每月 1 日自动扫描所有 Release，删除名为 `data.unity3d` 的多余旧附件，只保留最新（双保险，daily 用 `--clobber` 时只有一个，本工作流为空转）；
3. git 历史 / LFS 不再随游戏版本更新而膨胀。

> 背景：2026-10 迁移前，历史中已积累 4 个 `data.unity3d` 版本（约 282MB LFS）。若不清理它们，仅停止增长（仍有约 660MB 余量，按 2-4 次/年更新可用数年）；若希望立刻清掉，见下方“深度清理”。

## 可选：深度清理 git/LFS 历史中的旧版 data.unity3d

> 注意：这属于**重写历史**，会改变所有提交的 hash，需满足以下条件，且**无法由 CI 自动完成**（master 规则集禁止 force push）：
> - 必须先已用 `upload-data-unity3d.ps1` 上传最新原版包（构建依赖 Release 附件）；
> - 需要在 GitHub 网页**临时放开 master 规则集**（Settings → Rules → 允许 force push、暂时关闭“必须走合并队列”）；
> - 完成后所有协作者需**重新 clone**；
> - 建议在临时目录的全新克隆上执行。

```powershell
# 0) 前置确认：已上传最新附件；已临时放开 master 规则集

# 1) 全新克隆
git clone https://github.com/UnrealMultiple/TerrariaSinicization.git $env:TEMP\ts-purge
cd $env:TEMP\ts-purge
git lfs install --local

# 2) 安装 git-filter-repo 并移除历史中所有 data.unity3d
#    pip install git-filter-repo   或   winget install git-filter-repo
git filter-repo --path Resources/data.unity3d --invert-paths
git remote add origin https://github.com/UnrealMultiple/TerrariaSinicization.git

# 3) 强推（规则集已临时放开时执行）
git push --force origin master sh
git lfs push --all origin

# 4) 恢复规则集；在 GitHub 计费页确认 LFS 用量回落；通知协作者重新 clone
```

执行后仓库不再含任何 `data.unity3d` 版本，LFS 仅剩字体等对象（约 30MB）。