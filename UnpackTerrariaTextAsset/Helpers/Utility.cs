using System;
using System.IO;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnpackTerrariaTextAsset.Helpers;

public static class Utility
{
    public static string ReplaceInvalidPathChars(string filename)
    {
        return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
    }

    public static string GetFilePathWithoutExtension(string path)
    {
        string? directoryName = Path.GetDirectoryName(path);
        if (directoryName != null)
        {
            return Path.Combine(directoryName, Path.GetFileNameWithoutExtension(path));
        }

        return string.Empty;
    }

    public static string GetAssetsFileDirectory(AssetsFileInstance fileInst)
    {
        if (fileInst.parentBundle != null)
        {
            string dir = Path.GetDirectoryName(fileInst.parentBundle.path)!;

            string? upDir = Path.GetDirectoryName(dir);
            string? upDir2 = Path.GetDirectoryName(upDir ?? string.Empty);
            if (upDir != null && upDir2 != null)
            {
                if (Path.GetFileName(upDir) == "aa" && Path.GetFileName(upDir2) == "StreamingAssets")
                {
                    dir = Path.GetDirectoryName(upDir2)!;
                }
            }

            return dir;
        }
        else
        {
            string dir = Path.GetDirectoryName(fileInst.path)!;
            if (fileInst.name == "unity default resources" || fileInst.name == "unity_builtin_extra")
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return dir;
        }
    }

    public static DetectedFileType DetectFileType(string filePath)
    {
        using (FileStream fs = File.OpenRead(filePath))
        using (AssetsFileReader r = new AssetsFileReader(fs))
        {
            return DetectFileType(r, 0);
        }
    }

    public static DetectedFileType DetectFileType(AssetsFileReader r, long startAddress)
    {
        string possibleBundleHeader;
        int possibleFormat;
        string emptyVersion, fullVersion;

        r.BigEndian = true;

        if (r.BaseStream.Length < 0x20)
        {
            return DetectedFileType.Unknown;
        }
        r.Position = startAddress;
        possibleBundleHeader = r.ReadStringLength(7);
        r.Position = startAddress + 0x08;
        possibleFormat = r.ReadInt32();

        r.Position = startAddress + (possibleFormat >= 0x16 ? 0x30 : 0x14);

        string possibleVersion = "";
        char curChar;
        while (r.Position < r.BaseStream.Length && (curChar = (char)r.ReadByte()) != 0x00)
        {
            possibleVersion += curChar;
            if (possibleVersion.Length > 0xFF)
            {
                break;
            }
        }
        emptyVersion = Regex.Replace(possibleVersion, "[a-zA-Z0-9\\.\\n\\-]", "");
        fullVersion = Regex.Replace(possibleVersion, "[^a-zA-Z0-9\\.\\n\\-]", "");

        if (possibleBundleHeader == "UnityFS")
        {
            return DetectedFileType.BundleFile;
        }
        else if (possibleFormat < 0xFF && emptyVersion.Length == 0 && fullVersion.Length >= 5)
        {
            return DetectedFileType.AssetsFile;
        }
        return DetectedFileType.Unknown;
    }
}

public enum DetectedFileType
{
    Unknown,
    AssetsFile,
    BundleFile
}
