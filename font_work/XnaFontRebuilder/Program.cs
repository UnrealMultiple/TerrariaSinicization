using SixLabors.Fonts;
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
            string firstArg = args[0];
            if (firstArg == "--convert" || firstArg == "-c")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --convert <input.fnt> [output.txt] [options]");
                    Console.WriteLine("Options: --line-height <value>, --ascii-extra-spacing <value>, --character-spacing-compensation <value>");
                    return 1;
                }

                var options = ParseBaseConversionArgs(args.Skip(1).ToArray());
                ConvertBmFontToXnaTxt(options);
                Console.WriteLine($"Generated: {options.OutputPath}");
                return 0;
            }
            else if (firstArg == "--build-cfg-auto" || firstArg == "-bca")
            {
                if (args.Length < 4)
                {
                    Console.WriteLine("Usage: XnaFontRebuilder --build-cfg-auto <input.bin> <output.cfg> <fontPath> [--extra-chars <chars>] [--extra-chars-file <path>]");
                    return 1;
                }

                string inputPath = args[1];
                string outputPath = args[2];
                string fontPath = args[3];

                var extraOptions = ParseExtraCharsArgs(args.Skip(4).ToArray());
                BuildCfgAuto(inputPath, outputPath, fontPath, extraOptions);
                Console.WriteLine("Generated config: " + outputPath);
                return 0;
            }
            else
            {
                PrintUsage();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static string GetFontName(string path)
    {
        var description = FontDescription.LoadDescription(path);
        // BMFont 通过 Windows GDI (CreateFontW) 按“家族名”匹配字体，
        // 必须使用英文家族名（如 "Shanggu Round"）。若用 zh-CN 本地化名（如 "尙古圆体"），
        // 在 Windows Server Core 上可能无法匹配，导致回退到无 CJK 字形的系统字体、只生成 1 张纹理。
        return description.GetNameById(CultureInfo.InvariantCulture, SixLabors.Fonts.WellKnownIds.KnownNameIds.FontFamilyName);
    }

    #region 基础转换核心逻辑
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

        writer.Write(lineHeight);
        writer.Write(0);
        writer.Write((byte)1);
        writer.Write((byte)42);
        writer.Write((byte)0);
    }

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

        xAdvance = (int)(xAdvance + characterSpacingCompensation);
        if (id >= 33 && id <= 127)
        {
            xAdvance = (int)(xAdvance + (2f * asciiExtraSpacing));
            xOffset += asciiExtraSpacing;
        }

        writer.Write(x);
        writer.Write(y);
        writer.Write(width);
        writer.Write(height);
        writer.Write(0);                         // unknown, always 0
        writer.Write(yOffset);
        writer.Write(xAdvance);
        writer.Write(0);                         // unknown, always 0
        writer.Write((ushort)id);
        writer.Write(xOffset);
        writer.Write((float)width);              // stored as float
        writer.Write(((float)(xAdvance - width)) - xOffset); // kerning adjustment
        writer.Write(page);
    }

    static BaseConversionOptions ParseBaseConversionArgs(string[] args)
    {
        string inputPath = Path.GetFullPath(args[0]);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input FNT file not found.", inputPath);

        string outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + ".txt");

        int lineHeightOverride = 0;
        float asciiExtraSpacing = 0f;
        float characterSpacingCompensation = 0f;

        var remaining = args.Skip(1).ToList();
        bool outputSet = false;

        for (int i = 0; i < remaining.Count; i++)
        {
            string arg = remaining[i];
            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
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

    #region 自动配置生成
    static void BuildCfgAuto(string inputPath, string outputPath, string fontPath, ExtraCharsOptions extraOptions)
    {
        // 将所有路径解析为绝对路径，避免调用方工作目录不同导致找不到文件
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        fontPath = Path.GetFullPath(fontPath);

        List<ushort> ids = new List<ushort>();
        int lineHeight = 0;

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

            // 读取尾部 lineHeight（位于所有字符记录之后）
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                lineHeight = reader.ReadInt32();
            }
            else
            {
                lineHeight = 62; // fallback
                Console.WriteLine("Warning: No lineHeight found in file, using default 62.");
            }
        }

        // 合并配置中指定的额外字符（如额外汉字），去重后加入字符集
        List<ushort> extraIds = LoadExtraCharIds(extraOptions);
        if (extraIds.Count > 0)
        {
            var merged = new HashSet<ushort>(ids);
            merged.UnionWith(extraIds);
            ids = merged.ToList();
            Console.WriteLine($"Extra chars merged: +{extraIds.Count} distinct (total {ids.Count})");
            var preview = string.Concat(extraIds.Take(50).Select(id => char.ConvertFromUtf32(id)));
            Console.WriteLine($"Extra chars preview: {preview}");
        }

        GenerateCfg(ids, lineHeight, outputPath, fontPath, extraOptions.PageSize);
    }

    /// <summary>
    /// 解析 --build-cfg-auto 的额外参数（--extra-chars / --extra-chars-file / --page-size，均可重复出现）
    /// </summary>
    static ExtraCharsOptions ParseExtraCharsArgs(string[] args)
    {
        string extraChars = "";
        var extraCharFiles = new List<string>();
        int pageSize = 1024;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--extra-chars":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--extra-chars requires a value.");
                    extraChars += args[++i];
                    break;
                case "--extra-chars-file":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--extra-chars-file requires a value.");
                    extraCharFiles.Add(args[++i]);
                    break;
                case "--page-size":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--page-size requires a value.");
                    pageSize = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new ExtraCharsOptions(extraChars, extraCharFiles, pageSize);
    }

    /// <summary>
    /// 收集额外字符的码点：字符串 + 各 UTF-8 文件中的全部字符。
    /// 只保留 BMP（&lt;= U+FFFF，XNA 字体 ID 为 ushort），跳过空白与代理对超界字符。
    /// </summary>
    static List<ushort> LoadExtraCharIds(ExtraCharsOptions options)
    {
        var result = new List<ushort>();

        void AddText(string text, string source)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int codePoint;
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else
                {
                    codePoint = c;
                }

                // 跳过空白/控制字符（空格通常已存在于字体中，文件里的换行也不应被当作字形）
                if (codePoint < 0x20 || char.IsWhiteSpace((char)codePoint))
                    continue;
                if (codePoint > 0xFFFF)
                {
                    Console.WriteLine($"Warning: codepoint U+{codePoint:X4} (from {source}) is outside the BMP range supported by XNA fonts and was skipped.");
                    continue;
                }
                result.Add((ushort)codePoint);
            }
        }

        AddText(options.ExtraChars, "--extra-chars");
        foreach (string path in options.ExtraCharFiles)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"Warning: extra chars file not found: {fullPath}");
                continue;
            }
            using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            AddText(reader.ReadToEnd(), fullPath);
        }

        return result.Distinct().ToList();
    }

    /// <summary>
    /// 读取一个字符记录（与 WriteGlyphRecord 格式完全一致）
    /// </summary>
    static GlyphRecord ReadGlyphRecord(BinaryReader reader)
    {
        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int unknown1 = reader.ReadInt32(); // skip (always 0)
        int yOffset = reader.ReadInt32();
        int xAdvance = reader.ReadInt32();
        int unknown2 = reader.ReadInt32(); // skip (always 0)
        ushort id = reader.ReadUInt16();
        float xOffset = reader.ReadSingle();
        float floatWidth = reader.ReadSingle(); // skip
        float something = reader.ReadSingle();  // skip
        byte page = reader.ReadByte();

        return new GlyphRecord
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            YOffset = yOffset,
            XAdvance = xAdvance,
            Id = id,
            XOffset = xOffset,
            Page = page
        };
    }

    static void GenerateCfg(List<ushort> ids, int fontSize, string outputPath, string fontPath, int pageSize = 1024)
    {
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
        var fontName = GetFontName(fontPath);
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        writer.WriteLine("# AngelCode Bitmap Font Generator configuration file");
        writer.WriteLine("fileVersion=1");
        writer.WriteLine();
        writer.WriteLine("# font settings");
        writer.WriteLine($"fontName={fontName}");
        // BMFont 通过 Windows GDI (AddFontResourceEx) 加载字体，需要完整绝对路径，
        // 否则在 GitHub Actions 上可能解析失败并回退到系统默认字体，导致只生成 1 张纹理
        writer.WriteLine($"fontFile={fontPath}");
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
        writer.WriteLine($"outWidth={pageSize}");
        writer.WriteLine($"outHeight={pageSize}");
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
    #endregion

    #region 通用辅助方法
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
        Console.WriteLine("  XnaFontRebuilder --convert <input.fnt> [output.txt] [options]");
        Console.WriteLine("    Options: --line-height <value>, --ascii-extra-spacing <value>, --character-spacing-compensation <value>");
        Console.WriteLine("  XnaFontRebuilder --build-cfg-auto <input.bin> <output.cfg> <fontPath> [--extra-chars <chars>] [--extra-chars-file <path>]");
        Console.WriteLine("    Options: --extra-chars <chars> (repeatable, appended), --extra-chars-file <path> (repeatable, UTF-8)");
    }
    #endregion
}

internal sealed record BaseConversionOptions(
    string InputPath,
    string OutputPath,
    int LineHeightOverride,
    float AsciiExtraSpacing,
    float CharacterSpacingCompensation
);

internal sealed record ExtraCharsOptions(
    string ExtraChars,
    IReadOnlyList<string> ExtraCharFiles,
    int PageSize
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