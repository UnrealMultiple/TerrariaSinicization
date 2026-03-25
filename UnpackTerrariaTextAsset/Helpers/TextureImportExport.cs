using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace UnpackTerrariaTextAsset.Helpers;

/// <summary>
/// 纹理导入导出 - 从 UABEA TexturePlugin 改编
/// </summary>
public static class TextureImportExport
{
    public static byte[] Import(
        string imagePath, TextureFormat format,
        out int width, out int height, ref int mips,
        uint platform = 0, byte[] platformBlob = null)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);
        return Import(image, format, out width, out height, ref mips, platform, platformBlob);
    }

    public static byte[] Import(
        Image<Rgba32> image, TextureFormat format,
        out int width, out int height, ref int mips,
        uint platform = 0, byte[] platformBlob = null)
    {
        width = image.Width;
        height = image.Height;

        // can't make mipmaps from this image
        if (mips > 1 && (width != height || !TextureHelper.IsPo2(width)))
        {
            mips = 1;
        }

        image.Mutate(i => i.Flip(FlipMode.Vertical));

        byte[] encData = TextureEncoderDecoder.Encode(image, width, height, format, 5, mips);
        return encData;
    }

    public static bool Export(
        byte[] encData, string imagePath, int width, int height,
        TextureFormat format, uint platform = 0, byte[] platformBlob = null)
    {
        using Image<Rgba32> image = Export(encData, width, height, format, platform, platformBlob);
        if (image == null)
            return false;

        SaveImageAtPath(image, imagePath);
        return true;
    }

    public static Image<Rgba32> Export(
        byte[] encData, int width, int height,
        TextureFormat format, uint platform = 0, byte[] platformBlob = null)
    {
        byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
        if (decData == null)
            return null;

        Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
        image.Mutate(i => i.Flip(FlipMode.Vertical));

        return image;
    }

    public static void SaveImageAtPath(Image<Rgba32> image, string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".png":
                image.SaveAsPng(path);
                break;
            default:
                image.SaveAsPng(path);
                break;
        }
    }
}
