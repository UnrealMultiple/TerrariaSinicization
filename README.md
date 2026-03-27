# Terraria 汉化项目

为 Terraria 移动版提供完整的汉化解决方案！只需修改 `Localization` 文件夹中的翻译文件，即可自定义你的汉化效果。

## ✨ 功能特性

- 📝 **自定义汉化** - 只需修改 `Localization` 文件夹中的 JSON 文件，轻松实现个性化翻译
- 🔤 **字体替换** - 支持自定义中文字体，解决字体显示问题
- 🚀 **一键构建** - GitHub Actions 自动生成汉化资源包
- 📦 **资源导出/导入** - 完整的 Unity AssetBundle 处理工具链

## 🚀 快速开始

### 方法一：使用 GitHub Actions 自动构建（推荐）

1. **Fork 本项目**到你的 GitHub 账号
2. **修改** **`Localization`** **文件夹**中的翻译文件
3. **推送到 GitHub**，Actions 会自动运行
4. **下载成果**：在 Actions 运行完成后，从 "Artifacts" 中下载 `localized-data-unity3d`

### 方法二：本地构建

详细使用说明请查看各子项目文档：

- **[UnpackTerrariaTextAsset/README.md](./UnpackTerrariaTextAsset/README.md)** - 主工具使用文档
- **[font\_work/README.md](./font_work/README.md)** - 字体制作工具文档

## 📂 项目结构

```
UnpackTerrariaTextAsset/
├── UnpackTerrariaTextAsset/       # 主项目 - 资源包处理工具
├── font_work/                      # 字体制作工作区
│   └── XnaFontRebuilder/           # 字体格式转换工具
├── Localization/                    # ⭐ 汉化翻译文件（修改这里！）
│   ├── Base.json
│   ├── Game.json
│   ├── Items.json
│   ├── NPCs.json
│   └── ...
├── Resources/                       # 原始游戏资源
│   └── data.unity3d
└── .github/workflows/               # GitHub Actions 工作流
    └── build-and-localize.yml
```

## 📝 如何自定义汉化

1. **编辑翻译文件**：修改 `Localization` 文件夹中的 JSON 文件
2. **提交更改**：将修改推送到 GitHub
3. **等待构建**：GitHub Actions 会自动处理
4. **下载使用**：从 Actions 下载生成的 `data.unity3d`

## 📥 下载汉化资源

### 通过 GitHub Actions（推荐）

每次推送到仓库后，Actions 会自动运行并生成资源包：

1. 进入仓库的 **Actions** 标签页
2. 选择最新的工作流运行
3. 在页面底部的 **Artifacts** 区域找到 `localized-data-unity3d`
4. 点击下载即可获得汉化后的 `data.unity3d`

<br />

## 📱 修改 Terraria 安装包

按照以下步骤将汉化资源应用到 Terraria 游戏中：

### 步骤 1：准备汉化文件

下载 `localized-data-unity3d.zip` 文件后解压，将文件重命名为 `data.unity3d`

![步骤 1](./Images/Docs_1.png)
![步骤 2](./Images/Docs_2.png)

### 步骤 2：打开安装包

使用 MT 管理器找到你要修改的 Terraria 安装包，打开目录 `/assets/bin/Data/`，你会看到原有的 `data.unity3d`

![步骤 3](./Images/Docs_3.png)

### 步骤 3：替换文件

将下载的 `data.unity3d` 添加并替换掉原本安装包中的 `data.unity3d`

**重要提示**：替换时压缩级别必须选择 **"仅存储"**（不压缩）

![步骤 4](./Images/Docs_4.png)

### 步骤 4：重新签名

最后对修改后的安装包进行重新签名，汉化 Terraria 就完成了！

<br />

## 🤝 参与汉化，提交 PR

欢迎为这个汉化项目做出贡献！如果你想改进翻译或添加新内容，请按照以下步骤提交 Pull Request：

### 步骤 1：Fork 项目

1. 点击本项目右上角的 **Fork** 按钮
2. 将项目 Fork 到你的 GitHub 账号下

### 步骤 2：克隆并修改

```bash
# 克隆你的 Fork 仓库
git clone https://github.com/你的用户名/UnpackTerrariaTextAsset.git

# 进入项目目录
cd UnpackTerrariaTextAsset

# 创建一个新分支（可选但推荐）
git checkout -b improve-translation
```

### 步骤 3：修改翻译文件

编辑 `Localization` 文件夹中的 JSON 文件，改进或添加翻译内容。

### 步骤 4：提交更改

```bash
# 查看修改的文件
git status

# 添加修改的文件
git add Localization/

# 提交更改
git commit -m "改进翻译：简要说明你的修改"

# 推送到你的 Fork
git push origin improve-translation
```

### 步骤 5：创建 Pull Request

1. 回到你的 Fork 仓库页面
2. 点击 **Compare & pull request** 按钮
3. 填写 PR 标题和描述，说明你做了哪些改进
4. 点击 **Create pull request** 提交

### 注意事项

- 请保持翻译风格一致
- 确保 JSON 文件格式正确（可以使用在线 JSON 验证工具检查）
- 一次 PR 可以包含多个文件的修改，但请确保修改内容相关
- 提交前请先测试你的修改

感谢你的贡献！🎉

<br />

## 📚 详细文档

- **[UnpackTerrariaTextAsset/README.md](./UnpackTerrariaTextAsset/README.md)** - 资源处理工具完整文档
- **[font\_work/README.md](./font_work/README.md)** - 字体制作工具文档
- **[font\_work/XnaFontRebuilder/README.md](./font_work/XnaFontRebuilder/README.md)** - 字体格式转换工具

## 🛠️ 系统要求

- .NET 8.0 SDK/运行时（本地构建需要）
- Windows 操作系统
- GitHub 账号（使用 Actions 需要）

## 📄 许可证

本项目仅供学习和研究使用。

***

**⭐ 如果这个项目对你有帮助，请给个 Star！**
