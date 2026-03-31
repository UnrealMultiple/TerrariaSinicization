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
                    Console.WriteLine("Usage: XnaFontRebuilder --build-cfg-auto <input.bin> <output.cfg> <fontPath>");
                    return 1;
                }

                string inputPath = args[1];
                string outputPath = args[2];
                string fontPath = args[3];

                BuildCfgAuto(inputPath, outputPath, fontPath);
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
       return description.GetNameById(new CultureInfo("zh-CN"), SixLabors.Fonts.WellKnownIds.KnownNameIds.FontFamilyName);
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
    static void BuildCfgAuto(string inputPath, string outputPath, string fontPath)
    {
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

        GenerateCfg(ids, lineHeight, outputPath, fontPath);
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

    static void GenerateCfg(List<ushort> ids, int fontSize, string outputPath, string fontPath)
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
        writer.WriteLine($"fontFile={Path.GetFileName(fontPath)}");
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
        Console.WriteLine("  XnaFontRebuilder --build-cfg-auto <input.bin> <output.cfg> <fontPath>");
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