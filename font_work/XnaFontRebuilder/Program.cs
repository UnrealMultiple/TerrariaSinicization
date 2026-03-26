using System.Globalization;
using System.Text;
using System.Xml.Linq;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            // 处理命令模式
            string firstArg = args[0];
            if (firstArg == "-export" || firstArg == "-e")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --export <input.txt> [output.csv]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(inputPath, ".csv");
                ExportChars(inputPath, outputPath);
                Console.WriteLine("Exported: " + outputPath);
                return 0;
            }
            else if (firstArg == "simple" || firstArg == "-es")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --export-simple <input.txt> [output.csv]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(inputPath, ".csv");
                ExportCharsSimple(inputPath, outputPath);
                Console.WriteLine("Exported simple format: " + outputPath);
                return 0;
            }
            else if (firstArg == "-ascii" || firstArg == "-ea")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --export-ascii <input.txt> [output.txt]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(inputPath, ".txt");
                ExportCharsAsciiOnly(inputPath, outputPath);
                Console.WriteLine("Exported ASCII codes: " + outputPath);
                return 0;
            }
            else if (firstArg == "--export-text" || firstArg == "-et")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --export-text <input.bin> [output.txt]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(inputPath, ".txt");
                ExportCharsToTxt(inputPath, outputPath);
                Console.WriteLine("Exported characters to: " + outputPath);
                return 0;
            }
            else if (firstArg == "--build-cfg" || firstArg == "-bc")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --build-cfg <input.txt> <output.cfg> [--template <template.cfg>] [--fontsize <size>]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args[2];
                string templatePath = null;
                int? fontSize = null;

                for (int i = 3; i < args.Length; i++)
                {
                    if (args[i] == "--template" && i + 1 < args.Length)
                    {
                        templatePath = args[++i];
                    }
                    else if (args[i] == "--fontsize" && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int size))
                            fontSize = size;
                        else
                            Console.WriteLine("Warning: Invalid fontsize, using default.");
                    }
                }

                BuildCfg(inputPath, outputPath, templatePath, fontSize ?? 62);
                Console.WriteLine("Generated config: " + outputPath);
                return 0;
            }
            else if (firstArg == "--dump-all" || firstArg == "-da")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --dump-all <input.bin> [output.csv]");
                    return 1;
                }
                string inputPath = args[1];
                string outputPath = args.Length > 2 ? args[2] : null;
                DumpAllInfo(inputPath, outputPath);
                return 0;
            }
            else
            {
                // 基础转换模式：支持扩展参数
                var conversionOptions = ParseBaseConversionArgs(args);
                ConvertBmFontToXnaTxt(conversionOptions);
                Console.WriteLine($"Generated: {conversionOptions.OutputPath}");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 1;
        }
    }

    #region 基础转换核心逻辑（增强版）

    /// <summary>
    /// 将 AngelCode BMFont 的 .fnt 文件转换为 XNA 游戏可用的二进制格式
    /// </summary>
    /// <param name="options">转换参数</param>
    static void ConvertBmFontToXnaTxt(BaseConversionOptions options)
    {
        var document = XDocument.Load(options.InputPath, LoadOptions.None);
        var commonElement = document.Root?.Element("common")
            ?? throw new InvalidOperationException("Missing common element in FNT.");
        var charsElement = document.Root?.Element("chars")
            ?? throw new InvalidOperationException("Missing chars element in FNT.");
        var charElements = charsElement.Elements("char").ToList();

        byte pageCount = ParseByte(commonElement.Attribute("pages"), "common.pages");
        int lineHeight = options.LineHeightOverride != 0
            ? options.LineHeightOverride
            : ParseInt(commonElement.Attribute("lineHeight"), "common.lineHeight");
        int declaredCharCount = ParseInt(charsElement.Attribute("count"), "chars.count");

        using var output = new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(output);

        writer.Write(pageCount);
        writer.Write(declaredCharCount);

        foreach (var charElement in charElements)
        {
            WriteGlyphRecord(writer, charElement, options.AsciiExtraSpacing, options.CharacterSpacingCompensation);
        }

        // 写入尾部固定数据
        writer.Write(lineHeight);
        writer.Write(0);
        writer.Write((byte)1);
        writer.Write((byte)42);
        writer.Write((byte)0);
    }

    /// <summary>
    /// 将一个字符记录写入二进制流
    /// </summary>
    static void WriteGlyphRecord(BinaryWriter writer, XElement charElement, float asciiExtraSpacing, float characterSpacingCompensation)
    {
        int id = ParseInt(charElement.Attribute("id"), "char.id");
        int x = ParseInt(charElement.Attribute("x"), "char.x");
        int y = ParseInt(charElement.Attribute("y"), "char.y");
        int width = ParseInt(charElement.Attribute("width"), "char.width");
        int height = ParseInt(charElement.Attribute("height"), "char.height");
        float xOffset = ParseFloat(charElement.Attribute("xoffset"), "char.xoffset");
        int yOffset = ParseInt(charElement.Attribute("yoffset"), "char.yoffset");
        int xAdvance = ParseInt(charElement.Attribute("xadvance"), "char.xadvance");
        byte page = ParseByte(charElement.Attribute("page"), "char.page");

        // 应用字符间距补偿
        xAdvance = (int)(xAdvance + characterSpacingCompensation);

        // 为 ASCII 字符增加额外间距（例如拉丁字母）
        if (id >= 33 && id <= 127)
        {
            xAdvance = (int)(xAdvance + (2f * asciiExtraSpacing));
            xOffset += asciiExtraSpacing;
        }

        writer.Write(x);
        writer.Write(y);
        writer.Write(width);
        writer.Write(height);
        writer.Write(0);
        writer.Write(yOffset);
        writer.Write(xAdvance);
        writer.Write(0);
        writer.Write((ushort)id);
        writer.Write(xOffset);
        writer.Write((float)width);
        writer.Write(((float)(xAdvance - width)) - xOffset);
        writer.Write(page);
    }

    /// <summary>
    /// 解析基础转换的命令行参数
    /// </summary>
    static BaseConversionOptions ParseBaseConversionArgs(string[] args)
    {
        string inputPath = Path.GetFullPath(args[0]);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input FNT file not found.", inputPath);

        // 默认输出路径
        string outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + ".txt");

        int lineHeightOverride = 0;
        float asciiExtraSpacing = 0f;
        float characterSpacingCompensation = 0f;

        // 解析剩余参数（跳过第一个输入文件）
        var remaining = args.Skip(1).ToList();
        bool outputSet = false;

        for (int i = 0; i < remaining.Count; i++)
        {
            string arg = remaining[i];
            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                // 命名参数
                switch (arg)
                {
                    case "--output":
                    case "-o":
                        if (i + 1 >= remaining.Count)
                            throw new ArgumentException("--output requires a value.");
                        outputPath = Path.GetFullPath(remaining[++i]);
                        outputSet = true;
                        break;

                    case "--line-height":
                    case "--lineHeight":
                        if (i + 1 >= remaining.Count)
                            throw new ArgumentException("--line-height requires a value.");
                        lineHeightOverride = int.Parse(remaining[++i], CultureInfo.InvariantCulture);
                        break;

                    case "--latin-compensation":
                    case "--latinCompensation":
                    case "--ascii-extra-spacing":
                        if (i + 1 >= remaining.Count)
                            throw new ArgumentException("--latin-compensation requires a value.");
                        asciiExtraSpacing = float.Parse(remaining[++i], CultureInfo.InvariantCulture);
                        break;

                    case "--character-spacing-compensation":
                    case "--characterSpacingCompensation":
                    case "--char-spacing":
                        if (i + 1 >= remaining.Count)
                            throw new ArgumentException("--character-spacing-compensation requires a value.");
                        characterSpacingCompensation = float.Parse(remaining[++i], CultureInfo.InvariantCulture);
                        break;

                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }
            else
            {
                // 位置参数
                if (!outputSet)
                {
                    outputPath = Path.GetFullPath(arg);
                    outputSet = true;
                }
                else if (lineHeightOverride == 0)
                {
                    lineHeightOverride = int.Parse(arg, CultureInfo.InvariantCulture);
                }
                else if (asciiExtraSpacing == 0f)
                {
                    asciiExtraSpacing = float.Parse(arg, CultureInfo.InvariantCulture);
                }
                else if (characterSpacingCompensation == 0f)
                {
                    characterSpacingCompensation = float.Parse(arg, CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new ArgumentException("Too many positional arguments.");
                }
            }
        }

        return new BaseConversionOptions(inputPath, outputPath, lineHeightOverride, asciiExtraSpacing, characterSpacingCompensation);
    }

    #endregion

    #region 原有命令功能（保持不变）

    static void DumpAllInfo(string inputPath, string outputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(input);
        var output = outputPath != null
            ? new StreamWriter(outputPath, false, Encoding.UTF8)
            : null;

        try
        {
            var writer = output ?? Console.Out;

            byte pageCount = reader.ReadByte();
            int charCount = reader.ReadInt32();

            writer.WriteLine("=== XNA Font File Dump ===");
            writer.WriteLine($"Page Count: {pageCount}");
            writer.WriteLine($"Character Count: {charCount}");
            writer.WriteLine();

            if (output != null)
                writer.WriteLine("Id,IdHex,X,Y,Width,Height,YOffset,XAdvance,XOffset,Page");

            for (int i = 0; i < charCount; i++)
            {
                var glyph = ReadGlyphRecord(reader);
                if (output != null)
                {
                    writer.WriteLine($"{glyph.Id},{glyph.Id:X4},{glyph.X},{glyph.Y},{glyph.Width},{glyph.Height},{glyph.YOffset},{glyph.XAdvance},{glyph.XOffset},{glyph.Page}");
                }
                else
                {
                    writer.WriteLine($"Glyph {i + 1}:");
                    writer.WriteLine($"  ID: {glyph.Id} (0x{glyph.Id:X4})");
                    writer.WriteLine($"  Position: ({glyph.X}, {glyph.Y})");
                    writer.WriteLine($"  Size: {glyph.Width} x {glyph.Height}");
                    writer.WriteLine($"  Y Offset: {glyph.YOffset}");
                    writer.WriteLine($"  X Advance: {glyph.XAdvance}");
                    writer.WriteLine($"  X Offset: {glyph.XOffset}");
                    writer.WriteLine($"  Page: {glyph.Page}");
                    writer.WriteLine();
                }
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int lineHeight = reader.ReadInt32();
                int unknownInt = reader.ReadInt32();
                byte b1 = reader.ReadByte();
                byte b2 = reader.ReadByte();
                byte b3 = reader.ReadByte();

                writer.WriteLine("=== Trailer Data ===");
                writer.WriteLine($"Line Height: {lineHeight}");
                writer.WriteLine($"Unknown Int: {unknownInt}");
                writer.WriteLine($"Unknown Bytes: {b1}, {b2}, {b3}");
            }
            else
            {
                writer.WriteLine("=== No trailer data found (file might be incomplete) ===");
            }
        }
        finally
        {
            output?.Dispose();
        }
    }

    static void ExportCharsToTxt(string inputPath, string outputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(input);
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        reader.ReadByte(); // pageCount
        int charCount = reader.ReadInt32();

        for (int i = 0; i < charCount; i++)
        {
            var glyph = ReadGlyphRecord(reader);
            writer.Write((char)glyph.Id);
        }
    }

    static void BuildCfg(string inputPath, string outputPath, string templatePath, int fontSize)
    {
        List<ushort> ids = new List<ushort>();
        using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new BinaryReader(input))
        {
            reader.ReadByte(); // pageCount
            int charCount = reader.ReadInt32();
            for (int i = 0; i < charCount; i++)
            {
                var glyph = ReadGlyphRecord(reader);
                ids.Add(glyph.Id);
            }
        }

        ids.Sort();
        var ranges = new List<string>();
        int start = ids[0];
        int end = ids[0];
        for (int i = 1; i < ids.Count; i++)
        {
            if (ids[i] == end + 1)
            {
                end = ids[i];
            }
            else
            {
                ranges.Add(start == end ? start.ToString() : $"{start}-{end}");
                start = end = ids[i];
            }
        }
        ranges.Add(start == end ? start.ToString() : $"{start}-{end}");

        const int rangesPerLine = 13;
        var charLines = new List<string>();
        for (int i = 0; i < ranges.Count; i += rangesPerLine)
        {
            var group = ranges.Skip(i).Take(rangesPerLine);
            charLines.Add(string.Join(",", group));
        }

        if (templatePath != null)
        {
            string[] templateLines = File.ReadAllLines(templatePath);
            var outputLines = new List<string>();
            bool charsReplaced = false;
            bool fontSizeReplaced = false;

            foreach (string line in templateLines)
            {
                if (line.StartsWith("chars="))
                {
                    if (!charsReplaced)
                    {
                        outputLines.AddRange(charLines.Select(cl => "chars=" + cl));
                        charsReplaced = true;
                    }
                    continue;
                }
                else if (line.StartsWith("fontSize="))
                {
                    outputLines.Add($"fontSize={fontSize}");
                    fontSizeReplaced = true;
                    continue;
                }
                else
                {
                    outputLines.Add(line);
                }
            }

            if (!charsReplaced)
                outputLines.AddRange(charLines.Select(cl => "chars=" + cl));
            if (!fontSizeReplaced)
                outputLines.Add($"fontSize={fontSize}");

            File.WriteAllLines(outputPath, outputLines);
        }
        else
        {
            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                writer.WriteLine("# AngelCode Bitmap Font Generator configuration file");
                writer.WriteLine("fileVersion=1");
                writer.WriteLine();
                writer.WriteLine("# font settings");
                writer.WriteLine("fontName=思源黑体 CN");
                writer.WriteLine("fontFile=font.otf");
                writer.WriteLine("charSet=0");
                writer.WriteLine($"fontSize={fontSize}");
                writer.WriteLine("aa=4");
                writer.WriteLine("scaleH=100");
                writer.WriteLine("useSmoothing=1");
                writer.WriteLine("isBold=0");
                writer.WriteLine("isItalic=0");
                writer.WriteLine("useUnicode=1");
                writer.WriteLine("disableBoxChars=1");
                writer.WriteLine("outputInvalidCharGlyph=0");
                writer.WriteLine("dontIncludeKerningPairs=0");
                writer.WriteLine("useHinting=1");
                writer.WriteLine("renderFromOutline=0");
                writer.WriteLine("useClearType=1");
                writer.WriteLine("autoFitNumPages=0");
                writer.WriteLine("autoFitFontSizeMin=0");
                writer.WriteLine("autoFitFontSizeMax=0");
                writer.WriteLine();
                writer.WriteLine("# character alignment");
                writer.WriteLine("paddingDown=0");
                writer.WriteLine("paddingUp=0");
                writer.WriteLine("paddingRight=0");
                writer.WriteLine("paddingLeft=0");
                writer.WriteLine("spacingHoriz=1");
                writer.WriteLine("spacingVert=1");
                writer.WriteLine("useFixedHeight=0");
                writer.WriteLine("forceZero=0");
                writer.WriteLine("widthPaddingFactor=0.00");
                writer.WriteLine();
                writer.WriteLine("# output file");
                writer.WriteLine("outWidth=1024");
                writer.WriteLine("outHeight=1024");
                writer.WriteLine("outBitDepth=32");
                writer.WriteLine("fontDescFormat=1");
                writer.WriteLine("fourChnlPacked=0");
                writer.WriteLine("textureFormat=png");
                writer.WriteLine("textureCompression=0");
                writer.WriteLine("alphaChnl=0");
                writer.WriteLine("redChnl=3");
                writer.WriteLine("greenChnl=3");
                writer.WriteLine("blueChnl=3");
                writer.WriteLine("invA=0");
                writer.WriteLine("invR=0");
                writer.WriteLine("invG=0");
                writer.WriteLine("invB=0");
                writer.WriteLine();
                writer.WriteLine("# outline");
                writer.WriteLine("outlineThickness=0");
                writer.WriteLine();
                writer.WriteLine("# selected chars");
                foreach (string line in charLines)
                {
                    writer.WriteLine("chars=" + line);
                }
            }
        }
    }

    static void ExportChars(string inputPath, string outputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(input);
        using var output = new StreamWriter(outputPath, false, Encoding.UTF8);

        output.WriteLine("ASCII Code,Character,Description");

        reader.ReadByte(); // pageCount
        int charCount = reader.ReadInt32();

        for (int i = 0; i < charCount; i++)
        {
            var glyph = ReadGlyphRecord(reader);

            string charStr;
            string description;

            if (glyph.Id >= 32 && glyph.Id <= 126)
            {
                charStr = ((char)glyph.Id).ToString();
                description = GetAsciiDescription(glyph.Id);
            }
            else
            {
                charStr = "[0x" + glyph.Id.ToString("X4") + "]";
                description = "Non-printable or extended ASCII";
            }

            output.WriteLine($"{glyph.Id},{charStr},{description}");
        }
    }

    static void ExportCharsSimple(string inputPath, string outputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(input);
        using var output = new StreamWriter(outputPath, false, Encoding.UTF8);

        output.WriteLine("ASCII Code,Character");

        reader.ReadByte(); // pageCount
        int charCount = reader.ReadInt32();

        for (int i = 0; i < charCount; i++)
        {
            var glyph = ReadGlyphRecord(reader);
            string charStr = (glyph.Id >= 32 && glyph.Id <= 126)
                ? ((char)glyph.Id).ToString()
                : "[0x" + glyph.Id.ToString("X4") + "]";
            output.WriteLine($"{glyph.Id},{charStr}");
        }
    }

    static void ExportCharsAsciiOnly(string inputPath, string outputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(input);
        using var output = new StreamWriter(outputPath, false, Encoding.UTF8);

        reader.ReadByte(); // pageCount
        int charCount = reader.ReadInt32();

        var ids = new List<int>();
        for (int i = 0; i < charCount; i++)
        {
            var glyph = ReadGlyphRecord(reader);
            ids.Add(glyph.Id);
        }

        output.Write(string.Join(",", ids));
    }

    #endregion

    #region 通用辅助方法

    static GlyphRecord ReadGlyphRecord(BinaryReader reader)
    {
        return new GlyphRecord
        {
            X = reader.ReadInt32(),
            Y = reader.ReadInt32(),
            Width = reader.ReadInt32(),
            Height = reader.ReadInt32(),
            YOffset = reader.ReadInt32(),
            XAdvance = reader.ReadInt32(),
            Id = reader.ReadUInt16(),
            XOffset = reader.ReadSingle(),
            Page = reader.ReadByte()
        };
    }

    static string GetAsciiDescription(int code)
    {
        // 省略完整映射，与原始代码相同
        return code switch
        {
            32 => "Space",
            33 => "Exclamation mark",
            34 => "Double quote",
            35 => "Number sign",
            36 => "Dollar sign",
            37 => "Percent sign",
            38 => "Ampersand",
            39 => "Single quote",
            40 => "Left parenthesis",
            41 => "Right parenthesis",
            42 => "Asterisk",
            43 => "Plus sign",
            44 => "Comma",
            45 => "Hyphen",
            46 => "Period",
            47 => "Slash",
            48 => "Digit 0",
            49 => "Digit 1",
            50 => "Digit 2",
            51 => "Digit 3",
            52 => "Digit 4",
            53 => "Digit 5",
            54 => "Digit 6",
            55 => "Digit 7",
            56 => "Digit 8",
            57 => "Digit 9",
            58 => "Colon",
            59 => "Semicolon",
            60 => "Less than",
            61 => "Equals sign",
            62 => "Greater than",
            63 => "Question mark",
            64 => "At sign",
            65 => "Uppercase A",
            66 => "Uppercase B",
            67 => "Uppercase C",
            68 => "Uppercase D",
            69 => "Uppercase E",
            70 => "Uppercase F",
            71 => "Uppercase G",
            72 => "Uppercase H",
            73 => "Uppercase I",
            74 => "Uppercase J",
            75 => "Uppercase K",
            76 => "Uppercase L",
            77 => "Uppercase M",
            78 => "Uppercase N",
            79 => "Uppercase O",
            80 => "Uppercase P",
            81 => "Uppercase Q",
            82 => "Uppercase R",
            83 => "Uppercase S",
            84 => "Uppercase T",
            85 => "Uppercase U",
            86 => "Uppercase V",
            87 => "Uppercase W",
            88 => "Uppercase X",
            89 => "Uppercase Y",
            90 => "Uppercase Z",
            91 => "Left square bracket",
            92 => "Backslash",
            93 => "Right square bracket",
            94 => "Caret",
            95 => "Underscore",
            96 => "Grave accent",
            97 => "Lowercase a",
            98 => "Lowercase b",
            99 => "Lowercase c",
            100 => "Lowercase d",
            101 => "Lowercase e",
            102 => "Lowercase f",
            103 => "Lowercase g",
            104 => "Lowercase h",
            105 => "Lowercase i",
            106 => "Lowercase j",
            107 => "Lowercase k",
            108 => "Lowercase l",
            109 => "Lowercase m",
            110 => "Lowercase n",
            111 => "Lowercase o",
            112 => "Lowercase p",
            113 => "Lowercase q",
            114 => "Lowercase r",
            115 => "Lowercase s",
            116 => "Lowercase t",
            117 => "Lowercase u",
            118 => "Lowercase v",
            119 => "Lowercase w",
            120 => "Lowercase x",
            121 => "Lowercase y",
            122 => "Lowercase z",
            123 => "Left curly brace",
            124 => "Vertical bar",
            125 => "Right curly brace",
            126 => "Tilde",
            _ => "Unknown"
        };
    }

    static int ParseInt(XAttribute? attribute, string name)
    {
        if (attribute is null) throw new InvalidOperationException($"Missing attribute: {name}");
        return int.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    static float ParseFloat(XAttribute? attribute, string name)
    {
        if (attribute is null) throw new InvalidOperationException($"Missing attribute: {name}");
        return float.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    static byte ParseByte(XAttribute? attribute, string name)
    {
        if (attribute is null) throw new InvalidOperationException($"Missing attribute: {name}");
        return byte.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  XnaFontRebuilder <input.fnt> [output.txt] [lineHeightOverride] [asciiExtraSpacing] [characterSpacingCompensation]");
        Console.WriteLine("  XnaFontRebuilder <input.fnt> [output.txt] --line-height <value> --latin-compensation <value> --character-spacing-compensation <value>");
        Console.WriteLine("  XnaFontRebuilder -export <input.txt> [output.csv]");
        Console.WriteLine("  XnaFontRebuilder -export-simple <input.txt> [output.csv]");
        Console.WriteLine("  XnaFontRebuilder -export-ascii <input.txt> [output.txt]");
        Console.WriteLine("  XnaFontRebuilder --export-text <input.bin> [output.txt]");
        Console.WriteLine("  XnaFontRebuilder --build-cfg <input.txt> <output.cfg> [--template <template.cfg>] [--fontsize <size>]");
        Console.WriteLine("  XnaFontRebuilder --dump-all <input.bin> [output.csv]");
    }

    #endregion
}

/// <summary>
/// 基础转换模式的参数
/// </summary>
internal sealed record BaseConversionOptions(
    string InputPath,
    string OutputPath,
    int LineHeightOverride,
    float AsciiExtraSpacing,
    float CharacterSpacingCompensation
);

struct GlyphRecord
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public int YOffset;
    public int XAdvance;
    public ushort Id;
    public float XOffset;
    public byte Page;
}