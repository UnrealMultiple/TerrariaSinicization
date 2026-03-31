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
dotnet XnaFontRebuilder.dll --convert <input.fnt> [output.txt] [options]
```

**常用选项：**
- `--line-height <值>`: 覆盖行高
- `--latin-compensation <值>`: 拉丁字母额外间距补偿
- `--char-spacing <值>`: 全局字符间距补偿

**示例：**
```bash
# 基础转换
dotnet XnaFontRebuilder.dll --convert Combat_Text.fnt Combat_Text.txt

# 带额外选项
dotnet XnaFontRebuilder.dll --convert Combat_Text.fnt Combat_Text.txt --latin-compensation 0.5 --char-spacing 1
```

### 2. 自动配置生成（从 XNA 二进制字体文件）

从现有的 XNA 二进制字体文件（.txt）读取字符信息，生成 BMFont 配置文件：

```bash
dotnet XnaFontRebuilder.dll --build-cfg-auto <input.bin> <output.bmfc> <fontPath>
```

**参数：**
- `input.bin`: 输入的 XNA 二进制字体文件路径（.txt 格式）
- `output.bmfc`: 生成的 BMFont 配置文件路径
- `fontPath`: 源字体文件路径（用于获取字体名称，如 font.otf）

**工作原理：**
1. 读取 XNA 二进制文件中的所有字符 ID
2. 将字符 ID 排序并合并为连续范围（如 32-127, 161-511）
3. 读取文件尾部的 lineHeight 作为字体大小
4. 生成标准的 BMFont 配置文件（.bmfc）

**示例：**
```bash
# 从现有 XNA 字体文件生成 BMFont 配置
dotnet XnaFontRebuilder.dll --build-cfg-auto Death_Text.txt Death_Text.bmfc font.otf
```

## 项目结构

```
XnaFontRebuilder/
├── XnaFontRebuilder.csproj    # 项目文件
└── Program.cs                 # 主程序入口
```

## 使用场景

1. **字体转换**：将 BMFont 生成的 .fnt 文件转换为 Terraria 游戏可用的二进制 .txt 格式
2. **配置生成**：从现有的 XNA 二进制字体文件提取字符集，生成 BMFont 配置文件
3. **批量处理**：通过 FontBuilder.ps1 或 FontXnaBuilder.ps1 脚本批量处理多个字体

## 在 font_work 中的使用

XnaFontRebuilder 被以下脚本调用：

- **FontBuilder.ps1**: 使用 `--convert` 命令将 .fnt 转换为 .txt
- **FontXnaBuilder.ps1**: 使用 `--build-cfg-auto` 命令从 FontInfo/ 中的 XNA 字体文件生成 BMFont 配置

配置参数（如补偿值）在 `config.json` 的 `conversion` 部分定义。

## 注意事项

1. 确保输入的 .fnt 文件是 BMFont 生成的有效格式
2. 转换时建议使用 `--latin-compensation` 来调整拉丁字母的间距
3. 生成的 .txt 文件可以直接被 Terraria 游戏使用
4. `--build-cfg-auto` 生成的配置文件默认保存在根目录
5. `--build-cfg-auto` 的输入文件必须是 XNA 二进制格式（.txt），而不是字符信息文本文件
