using LibCpp2IL.Elf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace UnpackTerrariaTextAsset;

class Program
{
    static void Main(string[] args)
    {
        var Arguements = ParseArguements(Environment.GetCommandLineArgs());
        if (Arguements.TryGetValue("-export", out var target))
        {
            if (!File.Exists(target))
            {
                Console.WriteLine("目标文件不存在！");
                return;
            }

            var unpack = new UnpackBundle();
            unpack.OpenFiles(target);
            unpack.BatchExport();
        }

        if (Arguements.TryGetValue("-import", out var arg))
        {
            var sp = arg.Split(' ');
            if (sp.Length == 2)
            {
                var bundle = sp[0];
                var outPath = sp[1];
                var ins = new UnpackBundle();
                if (!File.Exists(bundle))
                {
                    Console.WriteLine($"未找到文件: {bundle}");
                    return;
                }
                ins.OpenFiles(bundle);
                ins.BatchImport();
                ins.SaveToMemory();
                ins.SaveBundle("temp");
                var cop = new UnpackBundle();
                cop.OpenFiles("temp");
                cop.CompressBundle(outPath, AssetsTools.NET.AssetBundleCompressionType.LZ4);
            }
        }

        if(Arguements.TryGetValue("-patch", out arg))
        {
            var sp = arg.Split(" ");
            var bundle = sp[0];
            var outPath = sp[1];
            var ins = new UnpackBundle();
            if (!File.Exists(bundle))
            {
                Console.WriteLine($"未找到文件: {bundle}");
                return;
            }
            ins.OpenFiles(bundle);
            ins.BatchExport();
            Sinicization();
            ins.BatchImport();
            ins.SaveToMemory();
            ins.SaveBundle("temp");
            var cop = new UnpackBundle();
            cop.OpenFiles("temp");
            cop.CompressBundle(outPath, AssetsTools.NET.AssetBundleCompressionType.LZ4);
        }

    }

    public static void Sinicization()
    {
        var files = Directory.GetFiles(UnpackBundle.ExportDir);
        var dic = files.Select(f => new { File = f, Name = Path.GetFileNameWithoutExtension(f) })
            .Where(x => x.Name.StartsWith("zh-Hans") || x.Name.StartsWith("fr-FR"))
            .GroupBy(x =>
            {
                var name = x.Name;
                var resourcePart = name.Contains('.') ? name[(name.IndexOf('.') + 1)..] : name[(name.IndexOf('-') + 1)..];
                var lastDashIndex = resourcePart.LastIndexOf('-');
                if (lastDashIndex > 0)
                {
                    resourcePart = resourcePart.Substring(0, lastDashIndex);
                }
                return resourcePart;
            })
            .Where(g => g.Count() == 2)
            .ToDictionary(
                g => Path.GetFileName(g.First(x => x.Name.StartsWith("zh-Hans")).File),
                g => Path.GetFileName(g.First(x => x.Name.StartsWith("fr-FR")).File)
            );
        foreach (var (zh_file, fr_file) in dic)
        {
            using var fs = new FileStream(Path.Combine(UnpackBundle.ImportDir, fr_file), FileMode.Create);
            fs.Write(File.ReadAllBytes(Path.Combine(UnpackBundle.ExportDir, zh_file)));
            fs.Close();
        }
        var en = files.FirstOrDefault(x => Path.GetFileName(x).StartsWith("en-US-resources.assets"));
        if (en == null)
            return;
        ModifyLanguage(en);
        var fr = dic.Values.FirstOrDefault(x => Path.GetFileName(x).StartsWith("fr-FR-resources.assets"));
        if (fr == null)
            return;
        ModifyLanguage(Path.Combine(UnpackBundle.ImportDir, fr));
        
    }

    static void ModifyLanguage(string file)
    {
        var content = File.ReadAllText(file);
        var json = JsonConvert.DeserializeObject<JObject>(content);
        json!["Language"]!["French"] = "简体中文";
        File.WriteAllText(Path.Combine(UnpackBundle.ImportDir, Path.GetFileName(file)), JsonConvert.SerializeObject(json, Formatting.Indented));
    }

    public static Dictionary<string, string> ParseArguements(string[] args)
    {
        string text = null;
        string text2 = "";
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Length == 0)
            {
                continue;
            }
            if (args[i][0] == '-' || args[i][0] == '+')
            {
                if (text != null)
                {
                    dictionary.Add(text.ToLower(), text2);
                    text2 = "";
                }
                text = args[i];
                text2 = "";
            }
            else
            {
                if (text2 != "")
                {
                    text2 += " ";
                }
                text2 += args[i];
            }
        }
        if (text != null)
        {
            dictionary.Add(text.ToLower(), text2);
            text2 = "";
        }
        return dictionary;
    }


}