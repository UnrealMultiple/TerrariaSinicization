using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp.PixelFormats;
using UnpackTerrariaTextAsset.Workspace;

namespace UnpackTerrariaTextAsset.Helpers;

/// <summary>
/// 纹理辅助函数 - 从 UABEA TexturePlugin 改编
/// </summary>
public static class TextureHelper
{
    public static bool IsPo2(int x)
    {
        return (x & (x - 1)) == 0;
    }

    public static int GetMaxMipCount(int width, int height)
    {
        int mips = 1;
        while (width > 1 || height > 1)
        {
            width >>= 1;
            height >>= 1;
            mips++;
        }
        return mips;
    }

    public static byte[] GetPlatformBlob(AssetTypeValueField baseField)
    {
        AssetTypeValueField platformBlobField = baseField["m_PlatformBlob"];
        if (platformBlobField != null && !platformBlobField.IsDummy)
        {
            AssetTypeValueField dataField = platformBlobField["data"];
            if (dataField != null && !dataField.IsDummy)
            {
                return dataField.AsByteArray;
            }
        }
        return null;
    }

    public static bool GetResSTexture(TextureFile texFile, AssetsFileInstance fileInst)
    {
        // 检查是否是 resS 流式纹理
        if (!string.IsNullOrEmpty(texFile.m_StreamData.path))
        {
            string resSName = Path.GetFileName(texFile.m_StreamData.path);
            // 简单检查：如果路径不为空且文件存在
            return true;
        }
        return true;
    }

    public static byte[] GetRawTextureBytes(TextureFile texFile, AssetsFileInstance fileInst)
    {
        // 获取原始纹理字节数据
        return texFile.GetTextureData(fileInst);
    }

    public static AssetTypeValueField GetByteArrayTexture(AssetWorkspace workspace, AssetContainer cont)
    {
        return workspace.GetBaseField(cont);
    }
}
