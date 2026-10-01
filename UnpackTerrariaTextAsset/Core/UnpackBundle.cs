using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using UnpackTerrariaTextAsset.Helpers;
using UnpackTerrariaTextAsset.Workspace;

namespace UnpackTerrariaTextAsset.Core;

public class UnpackBundle
{
    public BundleWorkspace Workspace { get; }
    public AssetsManager am { get => Workspace.am; }
    public BundleFileInstance BundleInst { get => Workspace.BundleInst!; }

    public AssetWorkspace AssetWorkspace { get; }

    public Dictionary<string, AssetContainer> LoadAssets { get; }

    public List<Tuple<AssetsFileInstance, byte[]>> ChangedAssetsDatas { get; set; }

    public const string ImportDir = "import";

    public const string ExportDir = "export";

    public UnpackBundle()
    {
        Workspace = new BundleWorkspace();
        AssetWorkspace = new AssetWorkspace(am, true);
        LoadAssets = [];
        ChangedAssetsDatas = new();
        if (!Directory.Exists(ImportDir))
        {
            Directory.CreateDirectory(ImportDir);
        }
        if (!Directory.Exists(ExportDir))
        {
            Directory.CreateDirectory(ExportDir);
        }
    }
    public void OpenFiles(string file)
    {
        string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
        am.LoadClassPackage(classDataPath);
        DetectedFileType fileType = Utility.DetectFileType(file);
        if (fileType == DetectedFileType.BundleFile)
        {
            BundleFileInstance bundleInst = am.LoadBundleFile(file, false);

            if (bundleInst.file.BlockAndDirInfo.BlockInfos.Any(inf => inf.GetCompressionType() != 0))
            {
                DecompressToMemory(bundleInst);
                LoadBundle(bundleInst);
            }
            else
            {
                LoadBundle(bundleInst);
            }

        }
        else
        {
            throw new FieldAccessException("This doesn't seem to be an assets file or bundle.");
        }
    }

    private void DecompressToMemory(BundleFileInstance bundleInst)
    {
        AssetBundleFile bundle = bundleInst.file;

        MemoryStream bundleStream = new MemoryStream();
        bundle.Unpack(new AssetsFileWriter(bundleStream));

        bundleStream.Position = 0;

        byte[] bundleBytes = bundleStream.ToArray();
        MemoryStream newBundleStream = new MemoryStream(bundleBytes);

        AssetBundleFile newBundle = new AssetBundleFile();
        newBundle.Read(new AssetsFileReader(newBundleStream));

        bundle.Close();
        bundleInst.file = newBundle;
    }

    private void LoadBundle(BundleFileInstance bundleInst)
    {
        Workspace.Reset(bundleInst);
        foreach (var file in Workspace.Files)
        {
            string name = file.Name;

            AssetBundleFile bundleFile = BundleInst.file;

            Stream assetStream = file.Stream;

            DetectedFileType fileType = Utility.DetectFileType(new AssetsFileReader(assetStream), 0);
            assetStream.Position = 0;

            if (fileType == DetectedFileType.AssetsFile)
            {
                string assetMemPath = Path.Combine(BundleInst.path, name);
                AssetsFileInstance fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);
                string uVer = fileInst.file.Metadata.UnityVersion;
                am.LoadClassDatabaseFromPackage(uVer);
                if (BundleInst != null && fileInst.parentBundle == null)
                    fileInst.parentBundle = BundleInst;
                AssetWorkspace.LoadAssetsFile(fileInst, true);

            }
        }
        SetupContainers(AssetWorkspace);
        AssetWorkspace.GenerateAssetsFileLookup();
        foreach (var asset in AssetWorkspace.LoadedAssets)
        {

            AssetContainer cont = asset.Value;
            AssetNameUtils.GetDisplayNameFast(AssetWorkspace, cont, true, out string assetName, out string typeName);
            assetName = Utility.ReplaceInvalidPathChars(assetName);
            var assetPath = $"{assetName}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}";
            LoadAssets.Add(assetPath, cont);
        }

    }

    public void BatchImport()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ImportDir);

        var files = Directory.GetFiles(dir);
        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string extension = Path.GetExtension(file).ToLower();
            
            if (LoadAssets.TryGetValue(fileName, out AssetContainer? cont) && cont != null)
            {
                AssetTypeValueField baseField = AssetWorkspace.GetBaseField(cont)!;
                
                if (cont.ClassId == 28 && extension == ".png")
                {
                    ImportTexture2D(baseField, file, cont);
                }
                else
                {
                    byte[] byteData = File.ReadAllBytes(file);
                    baseField["m_Script"].AsByteArray = byteData;

                    byte[] savedAsset = baseField.WriteToByteArray();

                    var replacer = new AssetsReplacerFromMemory(
                        cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
                    AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                }
            }
        }
    }

    private void ImportTexture2D(AssetTypeValueField baseField, string filePath, AssetContainer cont)
    {
        try
        {
            ApplyTextureFromFile(baseField, filePath, cont);

            byte[] savedAsset = baseField.WriteToByteArray();
            var replacer = new AssetsReplacerFromMemory(
                cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
            AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));

            Console.WriteLine($"导入纹理: {Path.GetFileName(filePath)} ({baseField["m_Width"].AsInt}x{baseField["m_Height"].AsInt}, 格式: {(TextureFormat)baseField["m_TextureFormat"].AsInt})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"导入纹理失败 {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }

    /// <summary>
    /// 将 PNG 文件编码为源纹理同格式的数据，并写入 baseField 的纹理字段（不注册 replacer）。
    /// </summary>
    private void ApplyTextureFromFile(AssetTypeValueField baseField, string filePath, AssetContainer cont)
    {
        TextureFormat fmt = (TextureFormat)baseField["m_TextureFormat"].AsInt;

        byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);
        uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

        int mips = baseField["m_MipCount"].AsInt;
        if (mips < 1) mips = 1;

        byte[] encImageBytes = TextureImportExport.Import(filePath, fmt, out int width, out int height, ref mips, platform, platformBlob);

        if (encImageBytes == null)
            throw new Exception($"无法编码纹理格式 {fmt}");

        TextureFormat finalFormat = fmt;
        if (fmt == TextureFormat.ETC_RGB4)
        {
            finalFormat = TextureFormat.DXT1;
            Console.WriteLine($"  格式转换: {fmt} -> {finalFormat}");
        }

        AssetTypeValueField m_StreamData = baseField["m_StreamData"];
        m_StreamData["offset"].AsInt = 0;
        m_StreamData["size"].AsInt = 0;
        m_StreamData["path"].AsString = "";

        if (!baseField["m_MipCount"].IsDummy)
            baseField["m_MipCount"].AsInt = mips;

        baseField["m_TextureFormat"].AsInt = (int)finalFormat;
        baseField["m_CompleteImageSize"].AsInt = encImageBytes.Length;
        baseField["m_Width"].AsInt = width;
        baseField["m_Height"].AsInt = height;

        AssetTypeValueField image_data = baseField["image data"];
        image_data.Value.ValueType = AssetValueType.ByteArray;
        image_data.TemplateField.ValueType = AssetValueType.ByteArray;
        image_data.AsByteArray = encImageBytes;
    }

    public void BatchExport()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExportDir);
        int textureCount = 0;
        int textAssetCount = 0;

        foreach (var (_, cont) in LoadAssets)
        {
            AssetTypeValueField baseField = AssetWorkspace.GetBaseField(cont)!;
            var name = baseField?["m_Name"]?.AsString;
            if (name == null) { continue; }

            name = Utility.ReplaceInvalidPathChars(name);
            string fileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}";

            if (cont.ClassId == 28)
            {
                ExportTexture2D(baseField, name, dir, fileName, cont);
                textureCount++;
            }
            else
            {
                var byteData = baseField?["m_Script"]?.AsByteArray;
                if (byteData == null) { continue; }

                string extension = ".json";
                string ucontExt = TextAssetHelper.GetUContainerExtension(cont);
                if (ucontExt != string.Empty)
                {
                    extension = ucontExt;
                }

                string file = Path.Combine(dir, $"{fileName}{extension}");

                File.WriteAllBytes(file, byteData);
                textAssetCount++;
            }
        }
        
        Console.WriteLine($"导出统计: {textAssetCount} 个文本资源, {textureCount} 个纹理资源");
    }

    private void ExportTexture2D(AssetTypeValueField baseField, string name, string dir, string fileName, AssetContainer cont)
    {
        try
        {
            TextureFile texFile = TextureFile.ReadTextureFile(baseField);

            if (texFile.m_Width == 0 && texFile.m_Height == 0)
            {
                Console.WriteLine($"警告: 纹理尺寸为 0x0: {name}");
                return;
            }

            if (!TextureHelper.GetResSTexture(texFile, cont.FileInstance))
            {
                string resSName = Path.GetFileName(texFile.m_StreamData.path);
                Console.WriteLine($"警告: resS 文件未找到: {resSName}");
                return;
            }

            byte[] data = TextureHelper.GetRawTextureBytes(texFile, cont.FileInstance);

            if (data == null)
            {
                string resSName = Path.GetFileName(texFile.m_StreamData.path);
                Console.WriteLine($"警告: resS 文件在磁盘上未找到: {resSName}");
                return;
            }

            byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);
            uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

            string file = Path.Combine(dir, $"{fileName}.png");
            bool success = TextureImportExport.Export(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat, platform, platformBlob);
            
            if (success)
            {
                Console.WriteLine($"导出纹理: {name} -> {fileName}.png ({texFile.m_Width}x{texFile.m_Height})");
            }
            else
            {
                string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                Console.WriteLine($"导出纹理失败 {name}: 无法解码纹理格式 {texFormat}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"导出纹理失败 {name}: {ex.Message}");
        }
    }

    public void CompressBundle(string path, AssetBundleCompressionType type)
    {
        using FileStream fs = File.Open(path, FileMode.Create);
        using AssetsFileWriter w = new AssetsFileWriter(fs);
        BundleInst.file.Pack(BundleInst.file.Reader, w, type, false);
    }

    public void SaveAndCompressBundle(string path, AssetBundleCompressionType type)
    {
        SaveToMemory();
        
        List<BundleReplacer> replacers = Workspace.GetReplacers();
        using MemoryStream ms = new MemoryStream();
        using AssetsFileWriter w = new AssetsFileWriter(ms);
        BundleInst.file.Write(w, replacers.ToList());
        
        ms.Position = 0;
        AssetBundleFile modifiedBundle = new AssetBundleFile();
        modifiedBundle.Read(new AssetsFileReader(ms));
        
        using FileStream fs = File.Open(path, FileMode.Create);
        using AssetsFileWriter fw = new AssetsFileWriter(fs);
        modifiedBundle.Pack(modifiedBundle.Reader, fw, type, false);
    }

    public void SaveToMemory()
    {
        var fileToReplacer = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();
        var changedFiles = AssetWorkspace.GetChangedFiles();
        foreach (var newAsset in AssetWorkspace.NewAssets)
        {
            AssetID assetId = newAsset.Key;
            AssetsReplacer replacer = newAsset.Value;
            string fileName = assetId.fileName;

            if (AssetWorkspace.LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file))
            {
                if (!fileToReplacer.ContainsKey(file))
                    fileToReplacer[file] = new List<AssetsReplacer>();

                fileToReplacer[file].Add(replacer);
            }
        }
        if (AssetWorkspace.fromBundle)
        {
            ChangedAssetsDatas.Clear();
            foreach (var file in changedFiles)
            {
                List<AssetsReplacer> replacers;
                if (fileToReplacer.ContainsKey(file))
                    replacers = fileToReplacer[file];
                else
                    replacers = new List<AssetsReplacer>(0);
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    file.file.Write(w, 0, replacers);
                    ChangedAssetsDatas.Add(new Tuple<AssetsFileInstance, byte[]>(file, ms.ToArray()));
                }
            }
        }

        List<Tuple<AssetsFileInstance, byte[]>> assetDatas = ChangedAssetsDatas;
        foreach (var tup in assetDatas)
        {
            AssetsFileInstance fileInstance = tup.Item1;
            byte[] assetData = tup.Item2;

            string assetName = Path.GetFileName(fileInstance.path);
            Workspace.AddOrReplaceFile(new MemoryStream(assetData), assetName, true);
            am.UnloadAssetsFile(fileInstance.path);

        }
    }

    public void SaveBundle(string path)
    {
        List<BundleReplacer> replacers = Workspace.GetReplacers();
        using FileStream fs = File.Open(path, FileMode.Create);
        using AssetsFileWriter w = new AssetsFileWriter(fs);
        BundleInst.file.Write(w, replacers.ToList());
    }


    private void SetupContainers(AssetWorkspace Workspace)
    {
        if (Workspace.LoadedFiles.Count == 0)
        {
            return;
        }

        UnityContainer ucont = new UnityContainer();
        foreach (AssetsFileInstance file in Workspace.LoadedFiles)
        {
            AssetsFileInstance? actualFile;
            AssetTypeValueField? ucontBaseField;
            if (UnityContainer.TryGetBundleContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
            {
                ucont.FromAssetBundle(am, actualFile, ucontBaseField);
            }
            else if (UnityContainer.TryGetRsrcManContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
            {
                ucont.FromResourceManager(am, actualFile, ucontBaseField);
            }
        }

        foreach (var asset in Workspace.LoadedAssets)
        {
            AssetPPtr pptr = new AssetPPtr(asset.Key.fileName, 0, asset.Key.pathID);
            string? path = ucont.GetContainerPath(pptr);
            if (path != null)
            {
                asset.Value.Container = path;
            }
        }
    }

    public void BatchLocalizationReplace(string localizationFolder)
    {
        var enUsBackups = new Dictionary<string, byte[]>();
        
        var languageNames = new Dictionary<string, string>
        {
            ["English"] = "简体中文",
            ["Spanish"] = "Español",
            ["French"] = "English",
            ["Italian"] = "Italiano",
            ["Russian"] = "Русский",
            ["Chinese"] = "简体中文",
            ["ChineseTraditional"] = "繁體中文",
            ["ChineseSimplified"] = "简体中文",
            ["Japanese"] = "日本語",
            ["Portuguese"] = "Português brasileiro",
            ["German"] = "Deutsch",
            ["Polish"] = "Polski",
            ["Korean"] = "한국어"
        };
        
        var langCodes = new[] { "en-US", "fr-FR", "es-ES", "de-DE", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "zh-Hans", "zh-Hant" };

        foreach (var (assetKey, cont) in LoadAssets)
        {
            var baseField = AssetWorkspace.GetBaseField(cont);
            if (baseField == null) continue;
            
            var mNameField = baseField["m_Name"];
            if (mNameField == null || mNameField.IsDummy) continue;
            
            var assetName = mNameField.AsString;
            if (string.IsNullOrEmpty(assetName)) continue;
            
            if (assetName.Contains("_comp")) continue;
            
            var mScriptField = baseField["m_Script"];
            if (mScriptField == null || mScriptField.IsDummy) continue;
            
            byte[]? byteData = null;
            try
            {
                byteData = mScriptField.AsByteArray;
            }
            catch
            {
                continue;
            }
            
            if (byteData == null) { continue; }

            var matchedLocalizationFile = MatchLocalizationFile(assetName, localizationFolder);

            if (assetName.StartsWith("en-US"))
            {
                enUsBackups[assetKey] = byteData;

                if (matchedLocalizationFile != null)
                {
                    byte[] newData = File.ReadAllBytes(matchedLocalizationFile);
                    mScriptField.AsByteArray = newData;
                    byte[] savedAsset = baseField.WriteToByteArray();
                    var replacer = new AssetsReplacerFromMemory(cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
                    AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                    Console.WriteLine($"Replaced: {assetName} with {Path.GetFileName(matchedLocalizationFile)}");
                }
            }
        }

        foreach (var (assetKey, cont) in LoadAssets)
        {
            var baseField = AssetWorkspace.GetBaseField(cont);
            if (baseField == null) continue;
            
            var mNameField = baseField["m_Name"];
            if (mNameField == null || mNameField.IsDummy) continue;
            
            var assetName = mNameField.AsString;
            if (string.IsNullOrEmpty(assetName) || !assetName.StartsWith("fr-FR")) { continue; }
            
            if (assetName.Contains("_comp")) continue;

            var matchingEnUsKey = FindMatchingEnUsAsset(assetName, LoadAssets.Keys);

            if (matchingEnUsKey != null && enUsBackups.TryGetValue(matchingEnUsKey, out byte[] enUsData))
            {
                var mScriptField = baseField["m_Script"];
                if (mScriptField == null || mScriptField.IsDummy) continue;
                
                try
                {
                    mScriptField.AsByteArray = enUsData;
                    ModifyLanguageInAsset(baseField);
                    byte[] savedAsset = baseField.WriteToByteArray();
                    var replacer = new AssetsReplacerFromMemory(cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
                    AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                    Console.WriteLine($"Replaced fr-FR: {assetName} with original en-US data");
                }
                catch
                {
                    continue;
                }
            }
        }

        foreach (var (assetKey, cont) in LoadAssets)
        {
            var baseField = AssetWorkspace.GetBaseField(cont);
            if (baseField == null) continue;
            
            var mNameField = baseField["m_Name"];
            if (mNameField == null || mNameField.IsDummy) continue;
            
            var assetName = mNameField.AsString;
            if (string.IsNullOrEmpty(assetName)) continue;
            
            if (assetName.Contains("_comp")) continue;
            
            var mScriptField = baseField["m_Script"];
            if (mScriptField == null || mScriptField.IsDummy) continue;
            
            byte[]? byteData = null;
            try
            {
                byteData = mScriptField.AsByteArray;
            }
            catch
            {
                continue;
            }
            
            if (byteData == null) { continue; }

            string? langCode = null;
            foreach (var code in langCodes)
            {
                if (assetName.StartsWith(code))
                {
                    if (code == "en-US" && assetName.StartsWith("en-US."))
                        continue;
                    if (!code.EndsWith("-US") && assetName.StartsWith(code + "."))
                        continue;
                    langCode = code;
                    break;
                }
            }

            if (langCode != null)
            {
                try
                {
                    ModifyAllLanguagesInAsset(baseField, languageNames);
                    byte[] savedAsset = baseField.WriteToByteArray();
                    var replacer = new AssetsReplacerFromMemory(cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
                    AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                    Console.WriteLine($"Updated language names: {assetName}");
                }
                catch
                {
                    continue;
                }
            }
        }
    }

    private string? MatchLocalizationFile(string assetName, string localizationFolder)
    {
        if (!Directory.Exists(localizationFolder))
        {
            return null;
        }

        var localizationFiles = Directory.GetFiles(localizationFolder, "*.json");

        string? category = ExtractCategory(assetName);

        if (category == null)
        {
            return null;
        }

        string targetFileName = category + ".json";

        foreach (var file in localizationFiles)
        {
            if (Path.GetFileName(file).Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    private string? ExtractCategory(string assetName)
    {
        if (assetName.StartsWith("en-US."))
        {
            var rest = assetName.Substring("en-US.".Length);
            var dotIndex = rest.IndexOf('.');
            if (dotIndex > 0)
            {
                return rest.Substring(0, dotIndex);
            }
            return rest;
        }
        else if (assetName.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            return "Base";
        }
        else if (assetName.StartsWith("en-US"))
        {
            return "Base";
        }

        return null;
    }

    private string? FindMatchingEnUsAsset(string frFrAssetName, IEnumerable<string> assetKeys)
    {
        string? enUsAssetNamePattern;

        if (frFrAssetName.StartsWith("fr-FR."))
        {
            enUsAssetNamePattern = "en-US." + frFrAssetName.Substring("fr-FR.".Length);
        }
        else if (frFrAssetName.StartsWith("fr-FR"))
        {
            enUsAssetNamePattern = "en-US" + frFrAssetName.Substring("fr-FR".Length);
        }
        else
        {
            return null;
        }

        foreach (var key in assetKeys)
        {
            if (key.StartsWith(enUsAssetNamePattern, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private void ModifyLanguageInAsset(AssetTypeValueField baseField)
    {
        try
        {
            var byteData = baseField["m_Script"].AsByteArray;
            if (byteData == null) return;

            string jsonContent = Encoding.UTF8.GetString(byteData);
            var json = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);

            if (json["Language"] != null && json["Language"]["French"] != null)
            {
                json["Language"]["French"] = "English";
                string modifiedJson = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
                baseField["m_Script"].AsByteArray = Encoding.UTF8.GetBytes(modifiedJson);
            }
        }
        catch
        {
        }
    }

    private void ModifyAllLanguagesInAsset(AssetTypeValueField baseField, Dictionary<string, string> languageNames)
    {
        try
        {
            var byteData = baseField["m_Script"].AsByteArray;
            if (byteData == null) return;

            string jsonContent = Encoding.UTF8.GetString(byteData);
            var json = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);

            if (json["Language"] != null)
            {
                foreach (var (key, value) in languageNames)
                {
                    if (json["Language"]![key] != null)
                    {
                        json["Language"]![key] = value;
                    }
                }
                string modifiedJson = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
                baseField["m_Script"].AsByteArray = Encoding.UTF8.GetBytes(modifiedJson);
            }
        }
        catch
        {
        }
    }

    public void DiffAndSyncLocalization(string localizationFolder)
    {
        if (!Directory.Exists(localizationFolder))
        {
            Directory.CreateDirectory(localizationFolder);
            Console.WriteLine($"Created localization folder: {localizationFolder}");
        }

        foreach (var (assetKey, cont) in LoadAssets)
        {
            var baseField = AssetWorkspace.GetBaseField(cont);
            if (baseField == null) continue;
            
            var mNameField = baseField["m_Name"];
            if (mNameField == null || mNameField.IsDummy) continue;
            
            var assetName = mNameField.AsString;
            if (string.IsNullOrEmpty(assetName)) continue;
            
            var mScriptField = baseField["m_Script"];
            if (mScriptField == null || mScriptField.IsDummy) continue;
            
            byte[]? byteData = null;
            try
            {
                byteData = mScriptField.AsByteArray;
            }
            catch
            {
                continue;
            }
            
            if (byteData == null) { continue; }

            if (assetName.StartsWith("zh-Hans"))
            {
                var category = ExtractCategoryFromZhHans(assetName);
                if (category != null)
                {
                    var localizationFile = Path.Combine(localizationFolder, $"{category}.json");
                    SyncJsonFiles(byteData, localizationFile, assetName);
                }
            }
        }
    }

    private string? ExtractCategoryFromZhHans(string assetName)
    {
        if (assetName.StartsWith("zh-Hans."))
        {
            var rest = assetName["zh-Hans.".Length..];
            var dotIndex = rest.IndexOf('.');
            if (dotIndex > 0)
            {
                return rest.Substring(0, dotIndex);
            }
            return rest;
        }
        else if (assetName.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            return "Base";
        }
        else if (assetName.StartsWith("zh-Hans"))
        {
            return "Base";
        }

        return null;
    }

    private void SyncJsonFiles(byte[] zhHansData, string localizationFile, string assetName)
    {
        try
        {
            string zhHansJson = Encoding.UTF8.GetString(zhHansData);
            var zhHansObj = Newtonsoft.Json.Linq.JObject.Parse(zhHansJson);

            if (!File.Exists(localizationFile))
            {
                File.WriteAllBytes(localizationFile, zhHansData);
                Console.WriteLine($"Created {Path.GetFileName(localizationFile)} from {assetName}");
                return;
            }

            string localizationJson = File.ReadAllText(localizationFile);
            var localizationObj = Newtonsoft.Json.Linq.JObject.Parse(localizationJson);

            int addedCount = 0;
            int removedCount = 0;
            SyncToken(zhHansObj, localizationObj, ref addedCount, ref removedCount);

            if (addedCount > 0 || removedCount > 0)
            {
                string outputJson = Newtonsoft.Json.JsonConvert.SerializeObject(localizationObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(localizationFile, outputJson);
                Console.WriteLine($"Synced {assetName} -> {Path.GetFileName(localizationFile)}: +{addedCount}, -{removedCount}");
            }
            else
            {
                Console.WriteLine($"No changes for {Path.GetFileName(localizationFile)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing {Path.GetFileName(localizationFile)}: {ex.Message}");
        }
    }

    // 递归键级同步：以 data.unity3d 导出的 zh-Hans JSON 为"键"的权威来源
    //  - 本地缺失的键（含深层，如 ItemName.DirtBlock）→ 从 zh-Hans 补齐（初始值=游戏官方中文，译者再改）
    //  - 本地多余的键（含深层，如 ItemName.OldKey）→ 删除
    //  - 叶子键值不同 → 保留本地已有翻译，不覆盖
    //  - 结构不一致（叶子/对象互换）→ 以 zh-Hans 为准，整棵替换（不计数）
    private static void SyncToken(Newtonsoft.Json.Linq.JObject source, Newtonsoft.Json.Linq.JObject target, ref int added, ref int removed)
    {
        // 1) 先删：target 有、source 没有的键（整棵子树删除）
        foreach (var prop in target.Properties().ToList())
        {
            if (source[prop.Name] == null)
            {
                prop.Remove();
                removed++;
            }
        }

        // 2) 再同步共有键：都是对象则递归深入；结构不一致时以 source 为准
        foreach (var prop in target.Properties().ToList())
        {
            var sourceProp = source[prop.Name];
            if (sourceProp == null)
            {
                continue;
            }

            if (prop.Value is Newtonsoft.Json.Linq.JObject targetObj &&
                sourceProp is Newtonsoft.Json.Linq.JObject sourceObj)
            {
                SyncToken(sourceObj, targetObj, ref added, ref removed);
            }
            else if (prop.Value.Type != sourceProp.Type)
            {
                // 游戏改了结构（叶子↔对象）：整棵换成 data.unity3d 的
                prop.Value = sourceProp.DeepClone();
            }
        }

        // 3) 最后补：source 有、target 没有的键
        foreach (var prop in source.Properties())
        {
            if (target[prop.Name] == null)
            {
                target[prop.Name] = prop.Value.DeepClone();
                added++;
            }
        }
    }

    public void BatchReplaceFonts(string fontWorkFolder)
    {
        if (!Directory.Exists(fontWorkFolder))
        {
            Console.WriteLine($"font_work 文件夹不存在: {fontWorkFolder}");
            return;
        }

        string[] fontFolders = { "Death_Text", "Combat_Crit", "Combat_Text", "Item_Stack", "Mouse_Text" };

        foreach (var fontName in fontFolders)
        {
            string fontFolder = Path.Combine(fontWorkFolder, fontName);
            if (!Directory.Exists(fontFolder))
            {
                Console.WriteLine($"跳过 {fontName}: 文件夹不存在");
                continue;
            }

            ProcessFontFolder(fontName, fontFolder);
        }
    }

    private void ProcessFontFolder(string fontName, string fontFolder)
    {
        Console.WriteLine($"正在处理字体: {fontName}");

        foreach (var (assetKey, cont) in LoadAssets)
        {
            var baseField = AssetWorkspace.GetBaseField(cont);
            if (baseField == null) continue;

            var mNameField = baseField["m_Name"];
            if (mNameField == null || mNameField.IsDummy) continue;

            var assetName = mNameField.AsString;
            if (string.IsNullOrEmpty(assetName)) continue;

            if (assetName.StartsWith(fontName))
            {
                if (assetName.Contains("_A") && cont.ClassId == 28)
                {
                    ReplaceFontTexture(assetKey, assetName, cont, baseField, fontName, fontFolder);
                }
                else if (assetName == fontName && cont.ClassId != 28)
                {
                    ReplaceFontJson(assetKey, cont, baseField, fontName, fontFolder);
                }
            }
        }

        // 若生成的字体页数超过 bundle 现有纹理页数，自动新增缺失的纹理页资产
        AddMissingFontTexturePages(fontName, fontFolder);
    }

    private void ReplaceFontTexture(string assetKey, string assetName, AssetContainer cont, AssetTypeValueField baseField, string fontName, string fontFolder)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(assetName, $@"{fontName}_(\d+)_A");
            if (!match.Success) return;

            if (!int.TryParse(match.Groups[1].Value, out int originalIndex)) return;

            int fontWorkIndex = originalIndex - 1;
            if (fontWorkIndex < 0)
            {
                Console.WriteLine($"跳过纹理 {assetName}: 序号无效");
                return;
            }

            string? targetFilePath = null;
            string? targetFileName = null;

            string twoDigitFileName = $"{fontName}_{fontWorkIndex:D2}.png";
            string twoDigitPath = Path.Combine(fontFolder, twoDigitFileName);
            if (File.Exists(twoDigitPath))
            {
                targetFilePath = twoDigitPath;
                targetFileName = twoDigitFileName;
            }
            else
            {
                string oneDigitFileName = $"{fontName}_{fontWorkIndex:D1}.png";
                string oneDigitPath = Path.Combine(fontFolder, oneDigitFileName);
                if (File.Exists(oneDigitPath))
                {
                    targetFilePath = oneDigitPath;
                    targetFileName = oneDigitFileName;
                }
            }

            if (targetFilePath == null || targetFileName == null)
            {
                Console.WriteLine($"跳过纹理 {assetName}: 未找到 {twoDigitFileName} 或 {fontName}_{fontWorkIndex:D1}.png");
                return;
            }

            Console.WriteLine($"替换纹理: {assetName} -> {targetFileName}");
            ImportTexture2D(baseField, targetFilePath, cont);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"替换纹理失败 {assetName}: {ex.Message}");
        }
    }

    private void ReplaceFontJson(string assetKey, AssetContainer cont, AssetTypeValueField baseField, string fontName, string fontFolder)
    {
        try
        {
            string targetFileName = $"{fontName}.txt";
            string targetFilePath = Path.Combine(fontFolder, targetFileName);

            if (!File.Exists(targetFilePath))
            {
                Console.WriteLine($"跳过 JSON {fontName}: 未找到 {targetFileName}");
                return;
            }

            Console.WriteLine($"替换 JSON: {fontName} -> {targetFileName}");
            byte[] newData = File.ReadAllBytes(targetFilePath);
            baseField["m_Script"].AsByteArray = newData;

            byte[] savedAsset = baseField.WriteToByteArray();
            var replacer = new AssetsReplacerFromMemory(cont.PathId, cont.ClassId, cont.MonoId, savedAsset);
            AssetWorkspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"替换 JSON 失败 {fontName}: {ex.Message}");
        }
    }

    /// <summary>
    /// 若新生成的字体文件（{fontName}.txt 首字节为页数）页数超过了 bundle 中已有的
    /// {fontName}_{N}_A 纹理数量，则为缺失的每一页创建新的 Texture2D 资产。
    /// 游戏按命名约定加载字体纹理页（TextAsset 中无纹理引用），因此新增同名纹理即可生效。
    /// </summary>
    private void AddMissingFontTexturePages(string fontName, string fontFolder)
    {
        try
        {
            string txtPath = Path.Combine(fontFolder, $"{fontName}.txt");
            if (!File.Exists(txtPath))
                return;

            byte[] fontData = File.ReadAllBytes(txtPath);
            if (fontData.Length < 1)
                return;
            int newPageCount = fontData[0];
            if (newPageCount <= 0)
                return;

            // 统计 bundle 中已有的纹理页，并取第 1 页纹理作为新建资产的模板
            int existingMax = 0;
            AssetContainer? sourceTexture = null;
            foreach (var (_, cont) in LoadAssets)
            {
                if (cont.ClassId != 28) continue;
                var baseField = AssetWorkspace.GetBaseField(cont);
                if (baseField == null) continue;
                string? name = baseField["m_Name"]?.AsString;
                if (string.IsNullOrEmpty(name)) continue;

                var match = System.Text.RegularExpressions.Regex.Match(name, $@"^{fontName}_(\d+)_A$");
                if (!match.Success) continue;

                int n = int.Parse(match.Groups[1].Value);
                if (n > existingMax) existingMax = n;
                if (n == 1) sourceTexture ??= cont;
            }

            if (newPageCount <= existingMax)
                return;

            if (sourceTexture == null)
            {
                Console.WriteLine($"跳过新增纹理页 {fontName}: bundle 中未找到该字体的纹理资产作为模板");
                return;
            }

            int missing = newPageCount - existingMax;
            Console.WriteLine($"新增纹理页: {fontName} 需要 {newPageCount} 页，bundle 现有 {existingMax} 页，将新增 {missing} 页");

            // 新资产 pathId 取该 assets 文件当前最大 pathId 之后的值
            long maxPathId = 0;
            foreach (var (_, cont) in LoadAssets)
            {
                if (cont.FileInstance.path == sourceTexture.FileInstance.path && cont.PathId > maxPathId)
                    maxPathId = cont.PathId;
            }

            for (int page = existingMax + 1; page <= newPageCount; page++)
            {
                int fontWorkIndex = page - 1; // bundle 第 N 页对应 font_work 的 {fontName}_{N-1}.png
                string? pngPath = FindFontPng(fontFolder, fontName, fontWorkIndex);
                if (pngPath == null)
                {
                    Console.WriteLine($"  跳过第 {page} 页纹理: 未找到 {fontName}_{fontWorkIndex}.png");
                    continue;
                }

                long newPathId = maxPathId + (page - existingMax);
                CreateFontTextureAsset(fontName, page, pngPath, sourceTexture, newPathId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"新增纹理页失败 {fontName}: {ex.Message}");
        }
    }

    private static string? FindFontPng(string fontFolder, string fontName, int index)
    {
        string twoDigit = Path.Combine(fontFolder, $"{fontName}_{index:D2}.png");
        if (File.Exists(twoDigit)) return twoDigit;

        string oneDigit = Path.Combine(fontFolder, $"{fontName}_{index:D1}.png");
        if (File.Exists(oneDigit)) return oneDigit;

        return null;
    }

    /// <summary>
    /// 以某张已有字体纹理为模板，创建一张新的 Texture2D 资产（新的 pathId）。
    /// </summary>
    private void CreateFontTextureAsset(string fontName, int pageNumber, string pngPath, AssetContainer sourceTexture, long newPathId)
    {
        try
        {
            AssetTypeValueField srcField = AssetWorkspace.GetBaseField(sourceTexture)!;
            AssetTypeTemplateField template = AssetWorkspace.GetTemplateField(sourceTexture);

            // 用源纹理的序列化字节重建一个全新的值字段（等价于深拷贝，不共享对象）
            byte[] srcBytes = srcField.WriteToByteArray();
            using var ms = new MemoryStream(srcBytes);
            using var reader = new AssetsFileReader(ms);
            AssetTypeValueField newField = template.MakeValue(reader, 0, null);

            // 填入 PNG 纹理数据并设置资产名称
            ApplyTextureFromFile(newField, pngPath, sourceTexture);
            newField["m_Name"].AsString = $"{fontName}_{pageNumber}_A";

            byte[] savedAsset = newField.WriteToByteArray();
            var replacer = new AssetsReplacerFromMemory(newPathId, 28, sourceTexture.MonoId, savedAsset);
            AssetWorkspace.AddReplacer(sourceTexture.FileInstance, replacer, new MemoryStream(savedAsset));

            Console.WriteLine($"新增纹理: {fontName}_{pageNumber}_A <- {Path.GetFileName(pngPath)} (pathId={newPathId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"新增纹理失败 {fontName}_{pageNumber}_A: {ex.Message}");
        }
    }
}
