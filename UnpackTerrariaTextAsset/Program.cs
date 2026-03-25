using UnpackTerrariaTextAsset.Core;

namespace UnpackTerrariaTextAsset;

class Program
{
    static void Main(string[] args)
    {
        var arguments = ParseArguments(Environment.GetCommandLineArgs());

        if (arguments.TryGetValue("-export", out var target))
        {
            HandleExport(target);
        }

        if (arguments.TryGetValue("-import", out var arg))
        {
            HandleImport(arg);
        }

        if (arguments.TryGetValue("-localize", out arg))
        {
            HandleLocalize(arg);
        }

        if (arguments.TryGetValue("-diff", out arg))
        {
            HandleDiff(arg);
        }
    }

    private static void HandleExport(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            Console.WriteLine("目标文件不存在！");
            return;
        }

        var unpack = new UnpackBundle();
        unpack.OpenFiles(targetPath);
        unpack.BatchExport();
        Console.WriteLine("导出完成！");
    }

    private static void HandleImport(string args)
    {
        var parts = args.Split(' ');
        if (parts.Length < 2)
        {
            Console.WriteLine("-import 参数格式错误！");
            Console.WriteLine("正确用法: -import <data.unity3d路径> <输出文件路径>");
            return;
        }

        var bundlePath = parts[0];
        var outputPath = parts[1];

        if (!File.Exists(bundlePath))
        {
            Console.WriteLine($"未找到文件: {bundlePath}");
            return;
        }

        ProcessAndSaveBundle(bundlePath, outputPath, unpack =>
        {
            unpack.BatchImport();
        });
    }

    private static void HandleLocalize(string args)
    {
        var parts = args.Split(' ');
        if (parts.Length < 3)
        {
            Console.WriteLine("-localize 参数格式错误！");
            Console.WriteLine("正确用法: -localize <data.unity3d路径> <本地化文件夹路径> <输出文件路径>");
            return;
        }

        var bundlePath = parts[0];
        var localizationFolder = parts[1];
        var outputPath = parts[2];

        if (!File.Exists(bundlePath))
        {
            Console.WriteLine($"未找到文件: {bundlePath}");
            return;
        }

        if (!Directory.Exists(localizationFolder))
        {
            Console.WriteLine($"未找到本地化文件夹: {localizationFolder}");
            return;
        }

        ProcessAndSaveBundle(bundlePath, outputPath, unpack =>
        {
            unpack.BatchLocalizationReplace(localizationFolder);
        });

        Console.WriteLine($"本地化完成！输出文件: {outputPath}");
    }

    private static void HandleDiff(string args)
    {
        var parts = args.Split(' ');
        if (parts.Length < 2)
        {
            Console.WriteLine("-diff 参数格式错误！");
            Console.WriteLine("正确用法: -diff <data.unity3d路径> <本地化文件夹路径>");
            return;
        }

        var bundlePath = parts[0];
        var localizationFolder = parts[1];

        if (!File.Exists(bundlePath))
        {
            Console.WriteLine($"未找到文件: {bundlePath}");
            return;
        }

        var unpack = new UnpackBundle();
        unpack.OpenFiles(bundlePath);
        unpack.DiffAndSyncLocalization(localizationFolder);
        Console.WriteLine("差异同步完成！");
    }

    private static void ProcessAndSaveBundle(string bundlePath, string outputPath, Action<UnpackBundle> processAction)
    {
        var unpack = new UnpackBundle();
        unpack.OpenFiles(bundlePath);
        processAction(unpack);
        unpack.SaveAndCompressBundle(outputPath, AssetsTools.NET.AssetBundleCompressionType.LZ4);
    }

    public static Dictionary<string, string> ParseArguments(string[] args)
    {
        string? currentOption = null;
        var currentValue = "";
        var dictionary = new Dictionary<string, string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Length == 0)
                continue;

            if (args[i][0] == '-' || args[i][0] == '+')
            {
                if (currentOption != null)
                {
                    dictionary.Add(currentOption.ToLowerInvariant(), currentValue);
                }
                currentOption = args[i];
                currentValue = "";
            }
            else
            {
                if (currentValue.Length > 0)
                {
                    currentValue += " ";
                }
                currentValue += args[i];
            }
        }

        if (currentOption != null)
        {
            dictionary.Add(currentOption.ToLowerInvariant(), currentValue);
        }

        return dictionary;
    }
}