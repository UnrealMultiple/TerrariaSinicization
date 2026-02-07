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
            unpack.BatchExportDump();
        }

        if (Arguements.TryGetValue("-import", out var outFile))
        {
            var sp = outFile.Split(' ');
            if (sp.Length == 2)
            {
                var bundle = sp[0];
                var outPath = sp[1];
                var ins = new UnpackBundle();
                ins.OpenFiles(bundle);
                ins.BatchImportDump();
                ins.SaveToMemory();
                ins.SaveBundle("temp");
                var cop = new UnpackBundle();
                cop.OpenFiles("temp");
                cop.CompressBundle(outPath, AssetsTools.NET.AssetBundleCompressionType.LZ4);
            }

        }

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