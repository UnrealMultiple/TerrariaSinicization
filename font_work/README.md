# font_work 字体制作工具

font_work 是一个完整的字体制作工作区，用于生成 Terraria 游戏所需的字体资源。

## 文件夹结构

```
font_work/
├── font.otf                          # 源字体文件（如思源黑体）
├── bmfont64.exe / bmfont64.com      # AngelCode BMFont 工具
├── Back/                             # BMFont 配置文件
│   ├── Combat_Crit.bmfc
│   ├── Combat_Text.bmfc
│   ├── Death_Text.bmfc
│   ├── Item_Stack.bmfc
│   └── Mouse_Text.bmfc
├── FontInfo/                         # 字体字符信息文件
│   ├── Combat_Crit.txt
│   ├── Combat_Text.txt
│   ├── Death_Text.txt
│   ├── Item_Stack.txt
│   └── Mouse_Text.txt
├── {字体名}/                         # 各字体的输出文件夹
│   ├── {字体名}.fnt                  # BMFont 生成的 XML 格式字体描述
│   ├── {字体名}.txt                  # XnaFontRebuilder 转换后的二进制格式
│   └── {字体名}_*.png                # 字体纹理图片
├── XnaFontRebuilder/                  # 字体格式转换工具
│   ├── XnaFontRebuilder.csproj
│   ├── Program.cs
│   └── README.md                     # XnaFontRebuilder 详细文档
├── FontBuilder.ps1                    # 字体构建脚本
└── FontXnaBuilder.ps1                 # Xna 字体构建脚本
```

## 支持的字体类型

项目预置了以下 5 种游戏字体的配置：

| 字体名称 | 用途 | 推荐字号 |
|---------|------|---------|
| Combat_Crit | 战斗暴击文字 | 适中 |
| Combat_Text | 战斗文本 | 适中 |
| Death_Text | 死亡文字 | 较大 |
| Item_Stack | 物品堆叠数量 | 较小 |
| Mouse_Text | 鼠标提示文字 | 25 |

## 系统要求

- .NET 8.0 SDK
- Windows 操作系统

## 字体制作流程

1. **准备源字体**：将你的字体文件命名为 `font.otf` 并放在 font_work 根目录

2. **生成字体**：运行 FontBuilder.ps1 脚本：
   ```powershell
   .\FontBuilder.ps1
   ```

3. **构建脚本会自动执行以下步骤**：
   - 检查必要文件
   - 使用 XnaFontRebuilder 生成 BMFont 配置文件
   - 使用 BMFont 生成 .fnt 文件和纹理图片
   - 将 .fnt 转换为 Terraria 可用的 .txt 格式

## FontBuilder.ps1 脚本使用说明

FontBuilder.ps1 是一个功能完整的字体批量生成工具，支持多种操作模式：

### 基本用法

```powershell
# 生成所有字体
.\FontBuilder.ps1

# 生成指定字体
.\FontBuilder.ps1 -Font Item_Stack

# 列出所有可用字体
.\FontBuilder.ps1 -List

# 显示帮助信息
.\FontBuilder.ps1 -Help

# 强制重新构建 XnaFontRebuilder 并生成所有字体
.\FontBuilder.ps1 -Rebuild
```

### 可用参数

| 参数 | 说明 |
|-----|------|
| 无参数 | 生成所有字体 |
| `-List` | 列出所有可用字体及其状态 |
| `-Help` | 显示详细帮助信息 |
| `-Font <名称>` | 只生成指定的字体 |
| `-Rebuild` | 强制重新构建 XnaFontRebuilder 工具 |

### 脚本功能特性

1. **环境检查**：自动检查 .NET SDK、BMFont、源字体等必要组件
2. **自动构建**：自动构建 XnaFontRebuilder 工具（如需要）
3. **配置生成**：从 FontInfo 目录的字符信息文件自动生成 BMFont 配置
4. **字体生成**：调用 BMFont 生成 .fnt 文件和纹理图片
5. **格式转换**：使用 XnaFontRebuilder 转换为 Terraria 可用的 .txt 格式
6. **输出验证**：验证生成的文件完整性并删除临时文件
7. **进度反馈**：提供详细的执行进度和结果统计

## FontXnaBuilder.ps1 脚本使用说明

FontXnaBuilder.ps1 是一个专门的工具，用于从现有的 XNA 二进制字体文件（.txt）直接生成 BMFont 配置文件。

### 功能特性

- 读取 XNA 二进制字体文件（.txt）中的字符信息
- 提取字符编码范围、行高等配置信息
- 自动生成对应的 BMFont 配置文件（.bmfc）
- 支持从现有字体文件中提取字体大小信息

### 基本用法

FontXnaBuilder.ps1 使用 XnaFontRebuilder 工具的 `--build-cfg-auto` 命令来实现此功能：

```powershell
# 从 XNA 二进制字体文件生成 BMFont 配置
dotnet .\XnaFontRebuilder\bin\Release\net8.0\XnaFontRebuilder.dll --build-cfg-auto <input.txt> <output.bmfc>

# 带模板文件生成配置
dotnet .\XnaFontRebuilder\bin\Release\net8.0\XnaFontRebuilder.dll --build-cfg-auto <input.txt> <output.bmfc> --template <template.bmfc>

# 指定字体大小
dotnet .\XnaFontRebuilder\bin\Release\net8.0\XnaFontRebuilder.dll --build-cfg-auto <input.txt> <output.bmfc> --fontsize <size>
```

### 使用场景

- 当你有原始的 XNA 字体文件，需要重新生成或修改字体时
- 需要从现有字体中提取字符集和配置信息时
- 想要基于原始字体创建自定义版本时

## XnaFontRebuilder 工具

XnaFontRebuilder 是一个专业的字体格式转换工具，主要功能包括：

### 基础转换（最常用）
将 BMFont 的 .fnt 文件转换为 Terraria 可用的二进制格式：
```bash
dotnet XnaFontRebuilder.dll --convert <input.fnt> [output.txt] [选项]
```

**常用选项：**
- `--line-height <值>`: 覆盖行高
- `--latin-compensation <值>`: 拉丁字母额外间距补偿
- `--character-spacing-compensation <值>`: 全局字符间距补偿

### 自动配置生成
从现有的字体文件自动生成 BMFont 配置文件：
```bash
dotnet XnaFontRebuilder.dll --build-cfg-auto <input.bin> <output.cfg> [--template <template.cfg>] [--fontsize <size>]
```

**注：** XnaFontRebuilder 工具的详细文档请参考 [XnaFontRebuilder/README.md](./XnaFontRebuilder/README.md)。

## 自定义字体制作

如需添加新的字体类型：

1. 在 FontInfo 目录中添加新的字符信息文件
2. 修改 FontBuilder.ps1 脚本，添加新字体的配置
3. 运行构建脚本生成新字体
4. 将生成的文件夹放入 font_work 目录，即可被 `-replacefonts` 或 `-build` 命令使用
