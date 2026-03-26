# UnpackTerrariaTextAsset

用于 Terraria 移动版 Unity 资源包的文本资源提取、汉化、纹理处理和字体替换工具。

## 功能特性

- 导出 Unity AssetBundle 中的 TextAsset 和 Texture2D 资源
- 导入修改后的 TextAsset 和 Texture2D 资源到 Unity AssetBundle
- 自定义本地化替换（使用自定义翻译文件替换 en-US 资源，并将原 en-US 资源替换到 fr-FR）
- 差异同步（-diff 命令，用于更新本地化文件，添加新内容、删除过时内容）
- 支持纹理资源的导入导出（PNG 格式），使用 UABEA 同款解码算法保证清晰度
- 支持 LZ4 压缩的 AssetBundle
- 字体替换（-replacefonts 命令，使用自定义字体纹理替换游戏中的字体）
- 自动跳过包含 "_comp" 的补充资源

## 系统要求

- .NET 8.0 运行时
- Windows 操作系统

## 快速开始

### 构建项目

```bash
dotnet build
```

### 文件夹结构

程序首次运行时会在执行目录下自动创建以下文件夹：

```
UnpackTerrariaTextAsset/
├── import/   # 存放要导入的资源文件
└── export/   # 存放导出的资源文件
```

## 使用方法

### 1. 导出资源

从 data.unity3d 中导出所有 TextAsset 和 Texture2D 资源：

```bash
UnpackTerrariaTextAsset.exe -export <data.unity3d路径>
```

导出的资源文件将保存在 `export` 文件夹中。
- TextAsset 资源：.json 或其他格式（根据资源类型）
- Texture2D 资源：.png 格式

### 2. 导入资源

将修改后的资源重新打包到 data.unity3d：

```bash
UnpackTerrariaTextAsset.exe -import <原data.unity3d路径> <输出文件路径>
```

**注意**：
- 将要替换的资源文件放在 `import` 文件夹中
- **不要更改导出资源文件的文件名**，否则无法正确替换
- 文件名格式通常为：`{资源名}-{assets文件名}-{路径ID}.扩展名`
- 支持导入 TextAsset（.json 等）和 Texture2D（.png）资源
- 包含 "_comp" 的补充资源会被自动跳过

### 3. 自定义本地化替换

使用自定义的翻译文件替换 en-US 资源，并将原始 en-US 资源替换到 fr-FR：

```bash
UnpackTerrariaTextAsset.exe -localize <data.unity3d路径> <本地化文件夹路径> <输出文件路径>
```

**本地化文件夹结构：**
```
localization/
├── Projectiles.json    # 对应 en-US.Projectiles
├── NPCs.json          # 对应 en-US.NPCs
├── Items.json         # 对应 en-US.Items
├── Town.json          # 对应 en-US.Town
├── PS4.json           # 对应 en-US.PS4
├── Switch.json        # 对应 en-US.Switch
├── XBO.json           # 对应 en-US.XBO
└── Base.json          # 对应 en-US
```

**此命令会：**
1. 用 localization 文件夹中的文件替换所有 en-US 开头的资源
2. 将替换前的原始 en-US 资源复制到 fr-FR 开头的资源
3. 修改所有语言资源中的语言显示名称
4. 跳过包含 "_comp" 的补充资源
5. 重新打包并压缩

**注意**：文件名匹配不依赖路径 ID，而是通过资源名称中的分类部分进行匹配。

### 4. 差异同步

从游戏更新后的 zh-Hans 语言文件与 localization 文件夹进行对比，自动添加新内容、删除过时内容：

```bash
UnpackTerrariaTextAsset.exe -diff <data.unity3d路径> <本地化文件夹路径>
```

**此命令会：**
1. 读取 data.unity3d 中的 zh-Hans 语言文件
2. 与 localization 文件夹中的对应文件进行对比
3. 添加 zh-Hans 中有但 localization 中没有的新内容
4. 删除 localization 中有但 zh-Hans 中没有的过时内容
5. 保存更新后的 localization 文件

### 5. 字体替换

使用 font_work 文件夹中的自定义字体纹理替换游戏中的字体：

```bash
UnpackTerrariaTextAsset.exe -replacefonts <data.unity3d路径> <font_work文件夹路径> <输出文件路径>
```

**此命令会：**
1. 读取 font_work 文件夹中的字体纹理和配置文件
2. 替换游戏中对应的字体资源
3. 重新打包并压缩

---

## font_work 字体制作工具

font_work 是一个完整的字体制作工作区，用于生成 Terraria 游戏所需的字体资源。

### font_work 文件夹结构

```
font_work/
├── font.otf                          # 源字体文件（如思源黑体）
├── bmfont64.exe / bmfont64.com      # AngelCode BMFont 工具
├── bmfont.bmfc                       # BMFont 通用配置模板
├── {字体名}.bmfc                     # 各字体的 BMFont 配置文件
│   ├── Combat_Crit.bmfc
│   ├── Combat_Text.bmfc
│   ├── Death_Text.bmfc
│   ├── Item_Stack.bmfc
│   └── Mouse_Text.bmfc
├── build-{字体名}-font.ps1           # 字体构建脚本（PowerShell）
│   ├── build-Combat_Crit-font.ps1
│   ├── build-Combat_Text-font.ps1
│   ├── build-Death_Text-font.ps1
│   ├── build-Item-Stack-font.ps1
│   └── build-Mouse_Text-font.ps1
├── {字体名}/                         # 各字体的输出文件夹
│   ├── {字体名}.fnt                  # BMFont 生成的 XML 格式字体描述
│   ├── {字体名}.txt                  # XnaFontRebuilder 转换后的二进制格式
│   └── {字体名}_*.png                # 字体纹理图片
└── XnaFontRebuilder/                  # 字体格式转换工具
    ├── XnaFontRebuilder.csproj
    └── Program.cs
```

### 支持的字体类型

项目预置了以下 5 种游戏字体的配置：

| 字体名称 | 用途 | 推荐字号 |
|---------|------|---------|
| Combat_Crit | 战斗暴击文字 | 适中 |
| Combat_Text | 战斗文本 | 适中 |
| Death_Text | 死亡文字 | 较大 |
| Item_Stack | 物品堆叠数量 | 较小 |
| Mouse_Text | 鼠标提示文字 | 25 |

### 字体制作流程

1. **准备源字体**：将你的字体文件命名为 `font.otf` 并放在 font_work 根目录

2. **配置 BMFont**：使用对应的 `.bmfc` 配置文件，或使用 `bmfont.bmfc` 作为模板进行自定义

3. **构建字体**：运行对应的 PowerShell 构建脚本：
   ```powershell
   .\build-Death_Text-font.ps1
   ```

4. **构建脚本会自动执行以下步骤**：
   - 检查必要文件
   - 使用 BMFont 生成 .fnt 文件和纹理图片
   - 构建 XnaFontRebuilder 工具
   - 将 .fnt 转换为 Terraria 可用的 .txt 格式

### XnaFontRebuilder 工具

XnaFontRebuilder 是一个专业的字体格式转换工具，主要功能包括：

#### 基础转换（最常用）
将 BMFont 的 .fnt 文件转换为 Terraria 可用的二进制格式：
```bash
dotnet XnaFontRebuilder.dll <input.fnt> [output.txt] [选项]
```

**常用选项：**
- `--line-height <值>`: 覆盖行高
- `--latin-compensation <值>`: 拉丁字母额外间距补偿
- `--character-spacing-compensation <值>`: 全局字符间距补偿

#### 其他功能
- `-export <input.txt> [output.csv]`: 导出字符信息到 CSV
- `-export-simple <input.txt> [output.csv]`: 导出简化版字符信息
- `-export-ascii <input.txt> [output.txt]`: 导出 ASCII 字符码列表
- `--export-text <input.bin> [output.txt]`: 从二进制文件导出字符
- `--build-cfg <input.txt> <output.cfg> [--template <template.cfg>] [--fontsize <size>]`: 生成 BMFont 配置
- `--dump-all <input.bin> [output.csv]`: 导出所有字体信息

### 自定义字体制作

如需添加新的字体类型：

1. 复制现有的 `.bmfc` 配置文件作为模板
2. 修改字体大小、字符集等参数
3. 复制并修改对应的 PowerShell 构建脚本
4. 运行构建脚本生成新字体
5. 将生成的文件夹放入 font_work 目录，即可被 `-replacefonts` 命令使用

## 项目结构

```
UnpackTerrariaTextAsset/
├── UnpackTerrariaTextAsset/       # 主项目
│   ├── Core/                       # 核心功能模块
│   │   └── UnpackBundle.cs         # 核心解压和资源处理类
│   ├── Helpers/                    # 辅助工具类
│   │   ├── AssetImportExport.cs    # 资产导入导出
│   │   ├── AssetNameUtils.cs       # 资产名称工具
│   │   ├── TextAssetHelper.cs      # 文本资产辅助
│   │   ├── TextureHelper.cs        # 纹理资源辅助（UABEA 源码）
│   │   ├── TextureImportExport.cs  # 纹理导入导出
│   │   ├── TextureEncoderDecoder.cs # 纹理编码解码
│   │   └── Utility.cs              # 通用工具类
│   ├── Workspace/                  # 工作空间管理
│   │   ├── AssetContainer.cs       # 资产容器类
│   │   ├── AssetWorkspace.cs       # 资产工作区
│   │   ├── BundleWorkspace.cs      # 资源包工作区
│   │   └── UnityContainer.cs       # Unity 容器
│   ├── Libs/                       # 第三方库
│   ├── Program.cs                  # 主程序入口
│   ├── UnpackTerrariaTextAsset.csproj
│   └── classdata.tpk               # Unity 类数据库
├── font_work/                      # 字体制作工作区
│   ├── XnaFontRebuilder/           # 字体格式转换工具
│   ├── {字体名}.bmfc               # BMFont 配置文件
│   ├── build-{字体名}-font.ps1     # 字体构建脚本
│   ├── {字体名}/                   # 各字体的输出文件夹
│   ├── bmfont64.exe                # AngelCode BMFont 工具
│   └── font.otf                    # 源字体文件
├── UnpackTerrariaTextAsset.slnx
└── README.md
```

## 技术栈

- **.NET 8.0** - 目标框架
- **AssetsTools.NET** - Unity 资源处理库
- **Newtonsoft.Json** - JSON 解析
- **SixLabors.ImageSharp** - 图像处理
- **BCnEncoder.Net** - 纹理压缩编码
- **AssetRipper.TextureDecoder** - Unity 纹理解码
- **UABEA TexturePlugin** - 纹理解码算法（已集成）

## 注意事项

1. **文件名**：导入时必须保持与导出时相同的文件名
2. **备份**：在进行任何修改前，请备份原始的 data.unity3d 文件
3. **格式**：确保修改后的资源文件格式与原始格式一致
4. **编码**：文本资源通常使用 UTF-8 编码
5. **纹理**：导入纹理时会自动编码为合适的 Unity 纹理格式，纹理导出使用与 UABEA 相同的解码算法，保证清晰
6. **_comp 资源**：包含 "_comp" 的补充资源会被自动跳过，不进行替换

## 许可证

本项目仅供学习和研究使用。
