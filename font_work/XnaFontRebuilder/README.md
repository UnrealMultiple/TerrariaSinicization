# XnaFontRebuilder

XnaFontRebuilder 是一个专业的字体格式转换工具，用于在 BMFont 和 XNA 二进制字体格式之间进行转换。

**注：** 此工具的原始项目来源于：[https://github.com/WindFrost-CSFT/XnaFontRebuilder](https://github.com/WindFrost-CSFT/XnaFontRebuilder)

## 系统要求

- .NET 8.0 SDK
- Windows 操作系统

## 构建项目

```bash
cd XnaFontRebuilder
dotnet build -c Release
```

## 主要功能

### 1. 基础转换（最常用）

将 BMFont 的 .fnt 文件转换为 Terraria 可用的二进制格式：

```bash
dotnet XnaFontRebuilder.dll --convert <input.fnt> [output.txt] [选项]
```

**常用选项：**
- `--line-height <值>`: 覆盖行高
- `--latin-compensation <值>`: 拉丁字母额外间距补偿
- `--character-spacing-compensation <值>`: 全局字符间距补偿

**示例：**
```bash
# 基础转换
dotnet XnaFontRebuilder.dll --convert Combat_Text.fnt Combat_Text.txt

# 带额外选项
dotnet XnaFontRebuilder.dll --convert Combat_Text.fnt Combat_Text.txt --latin-compensation 0.5 --char-spacing 1
```

### 2. 自动配置生成

从现有的字体文件自动生成 BMFont 配置文件：

```bash
dotnet XnaFontRebuilder.dll --build-cfg-auto <input.bin> <output.cfg> [--template <template.cfg>] [--fontsize <size>]
```

**选项：**
- `--template <template.cfg>`: 使用模板配置文件
- `--fontsize <size>`: 指定字体大小

**示例：**
```bash
# 从现有字体文件生成配置
dotnet XnaFontRebuilder.dll --build-cfg-auto Combat_Text.txt Combat_Text.bmfc

# 带模板文件
dotnet XnaFontRebuilder.dll --build-cfg-auto Combat_Text.txt Combat_Text.bmfc --template .\Back\Combat_Text.bmfc

# 指定字体大小
dotnet XnaFontRebuilder.dll --build-cfg-auto Combat_Text.txt Combat_Text.bmfc --fontsize 62
```

### 3. 从字符信息生成配置

从包含字符信息的文本文件生成 BMFont 配置：

```bash
dotnet XnaFontRebuilder.dll --build-cfg-auto <char-info.txt> <output.cfg> [--from-font <existing-font.txt>]
```

**选项：**
- `--from-font <existing-font.txt>`: 从现有字体文件中检测字体大小

## 项目结构

```
XnaFontRebuilder/
├── XnaFontRebuilder.csproj    # 项目文件
└── Program.cs                   # 主程序入口
```

## 使用场景

1. **字体转换**：将 BMFont 生成的 .fnt 文件转换为 Terraria 游戏可用的二进制 .txt 格式
2. **配置生成**：从现有字体文件或字符信息自动生成 BMFont 配置文件
3. **批量处理**：通过 FontBuilder.ps1 脚本批量处理多个字体

## 注意事项

1. 确保输入的 .fnt 文件是 BMFont 生成的有效格式
2. 转换时建议使用 `--latin-compensation` 来调整拉丁字母的间距
3. 生成的 .txt 文件可以直接被 UnpackTerrariaTextAsset 的 `-replacefonts` 或 `-build` 命令使用
