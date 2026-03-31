# font_work 字体制作工具

font_work 是一个完整的字体制作工作区，用于生成 Terraria 游戏所需的字体资源。

## 文件夹结构

```
font_work/
├── config.json                        # 字体配置文件（全局配置和字体配置）
├── font.otf / ShangguRound-Bold.ttf   # 源字体文件
├── bmfont64.exe / bmfont64.com        # AngelCode BMFont 工具
├── Back/                              # 备份的 BMFont 配置文件
│   ├── Combat_Crit.bmfc
│   ├── Combat_Text.bmfc
│   ├── Death_Text.bmfc
│   ├── Item_Stack.bmfc
│   └── Mouse_Text.bmfc
├── FontInfo/                          # 字体字符信息文件
│   ├── Combat_Crit.txt
│   ├── Combat_Text.txt
│   ├── Death_Text.txt
│   ├── Item_Stack.txt
│   └── Mouse_Text.txt
├── {字体名}/                          # 各字体的输出文件夹
│   ├── {字体名}.fnt                   # BMFont 生成的 XML 格式字体描述
│   ├── {字体名}.txt                   # XnaFontRebuilder 转换后的二进制格式
│   └── {字体名}_*.png                 # 字体纹理图片
├── XnaFontRebuilder/                  # 字体格式转换工具
│   ├── XnaFontRebuilder.csproj
│   ├── Program.cs
│   └── README.md                      # XnaFontRebuilder 详细文档
├── FontBuilder.ps1                    # 字体构建脚本（使用 Back/ 下的配置文件）
└── FontXnaBuilder.ps1                 # Xna 字体构建脚本（从 FontInfo/ 生成配置）
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

## 配置文件说明 (config.json)

所有字体配置都存储在 `config.json` 文件中：

```json
{
  "global": {
    "bmfontExe": "./bmfont64.com",
    "xnaFontRebuilder": "./XnaFontRebuilder/bin/Release/net8.0/XnaFontRebuilder.dll",
    "sourceFont": "./ShangguRound-Bold.ttf",
    "fontInfoDir": "./FontInfo"
  },
  "fonts": {
    "Death_Text": {
      "configFile": "./Death_Text.bmfc",
      "outputDir": "./Death_Text",
      "fontFile": "Death_Text.fnt",
      "txtFile": "Death_Text.txt",
      "description": "死亡文字字体",
      "charInfoFile": "./FontInfo/Death_Text.txt"
    }
    // ... 其他字体配置
  },
  "conversion": {
    "latinCompensation": 0.5,
    "charSpacing": 1
  }
}
```

### 配置项说明

**global** - 全局配置：
- `bmfontExe`: BMFont 可执行文件路径
- `xnaFontRebuilder`: XnaFontRebuilder DLL 路径
- `sourceFont`: 源字体文件路径（所有字体共用）
- `fontInfoDir`: 字体字符信息目录

**fonts** - 字体配置（每种字体）：
- `configFile`: BMFont 配置文件路径（生成/使用）
- `outputDir`: 字体输出目录
- `fontFile`: 生成的 .fnt 文件名
- `txtFile`: 生成的 .txt 文件名
- `description`: 字体描述
- `charInfoFile`: 字符信息文件路径（FontXnaBuilder 使用）

**conversion** - 转换参数：
- `latinCompensation`: 拉丁字母额外间距补偿
- `charSpacing`: 全局字符间距补偿

## 字体制作流程

### 方式一：使用 FontBuilder.ps1（推荐，使用现有配置）

适用于：使用 `Back/` 目录下预置的 BMFont 配置文件

1. **准备源字体**：将你的字体文件命名为 `font.otf`（或修改 config.json 中的 `sourceFont`）并放在 font_work 根目录

2. **生成字体**：
   ```powershell
   .\FontBuilder.ps1
   ```

3. **脚本会自动执行**：
   - 检查必要文件
   - 使用 BMFont 生成 .fnt 文件和纹理图片
   - 将 .fnt 转换为 Terraria 可用的 .txt 格式

### 方式二：使用 FontXnaBuilder.ps1（从 XNA 字体文件生成）

适用于：从 `FontInfo/` 目录的 XNA 二进制字体文件动态生成 BMFont 配置

1. **准备源字体**：同上

2. **生成字体**：
   ```powershell
   .\FontXnaBuilder.ps1
   ```

3. **脚本会自动执行**：
   - 从 FontInfo/ 读取 XNA 二进制字体文件（.txt）
   - 使用 XnaFontRebuilder `--build-cfg-auto` 提取字符信息并生成 BMFont 配置文件
   - 使用 BMFont 生成 .fnt 文件和纹理图片
   - 将 .fnt 转换为 Terraria 可用的 .txt 格式
   - 删除临时配置文件

**注意**：FontInfo/ 目录中的 `.txt` 文件必须是 XNA 二进制格式的字体文件，工具会从中提取字符 ID 范围和 lineHeight 信息。

## 脚本使用说明

### FontBuilder.ps1

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

### FontXnaBuilder.ps1

```powershell
# 生成所有字体
.\FontXnaBuilder.ps1

# 生成指定字体
.\FontXnaBuilder.ps1 -Font Death_Text

# 列出所有可用字体
.\FontXnaBuilder.ps1 -List

# 显示帮助信息
.\FontXnaBuilder.ps1 -Help

# 强制重新构建 XnaFontRebuilder 并生成所有字体
.\FontXnaBuilder.ps1 -Rebuild
```

### 可用参数

| 参数 | 说明 |
|-----|------|
| 无参数 | 生成所有字体 |
| `-List` | 列出所有可用字体及其状态 |
| `-Help` | 显示详细帮助信息 |
| `-Font <名称>` | 只生成指定的字体 |
| `-Rebuild` | 强制重新构建 XnaFontRebuilder 工具 |

## XnaFontRebuilder 工具

XnaFontRebuilder 是一个专业的字体格式转换工具，详细文档请参考 [XnaFontRebuilder/README.md](./XnaFontRebuilder/README.md)。

### 常用命令

```bash
# 转换 .fnt 为 .txt
dotnet XnaFontRebuilder.dll --convert input.fnt output.txt --latin-compensation 0.5 --char-spacing 1

# 从 XNA 二进制字体文件生成 BMFont 配置
dotnet XnaFontRebuilder.dll --build-cfg-auto input.txt output.bmfc font.ttf
```

## 自定义字体制作

### 添加新字体

1. 在 `config.json` 的 `fonts` 部分添加新字体配置
2. 在 `FontInfo/` 目录中添加字符信息文件（如使用 FontXnaBuilder）
3. 或在 `Back/` 目录中添加 BMFont 配置文件（如使用 FontBuilder）
4. 运行构建脚本生成新字体

### 修改现有字体

直接编辑 `config.json` 文件：
- 修改 `global.sourceFont` 更换源字体
- 修改 `conversion` 参数调整转换效果
- 修改字体配置调整输出路径等

## 注意事项

1. **路径格式**：config.json 中使用 `/` 作为路径分隔符
2. **源字体**：所有字体共用 `global.sourceFont` 指定的字体文件
3. **配置文件**：FontXnaBuilder 生成的配置文件保存在根目录，使用后会自动删除
4. **字符信息**：FontInfo/ 目录中的文件包含字体需要的字符编码信息
