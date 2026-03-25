using AssetsTools.NET.Texture;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace UnpackTerrariaTextAsset.Helpers;

/// <summary>
/// 纹理编码解码器 - 从 UABEA TexturePlugin 改编，添加了 BCnEncoder 支持
/// </summary>
public static class TextureEncoderDecoder
{
    // needs organization
    public static int RGBAToFormatByteSize(TextureFormat format, int width, int height)
    {
        int blockCountX, blockCountY;
        switch (format)
        {
            case TextureFormat.RGB9e5Float:
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
            case TextureFormat.RGBA32:
                return width * height * 4;
            case TextureFormat.RGB24:
                return width * height * 3;
            case TextureFormat.ARGB4444:
            case TextureFormat.RGBA4444:
            case TextureFormat.RGB565:
                return width * height * 2;
            case TextureFormat.Alpha8:
            case TextureFormat.R8:
                return width * height;
            case TextureFormat.R16:
            case TextureFormat.RG16:
            case TextureFormat.RHalf:
                return width * height * 2;
            case TextureFormat.RGHalf:
                return width * height * 4;
            case TextureFormat.RGBAHalf:
                return width * height * 8;
            case TextureFormat.RFloat:
                return width * height * 4;
            case TextureFormat.RGFloat:
                return width * height * 8;
            case TextureFormat.RGBAFloat:
                return width * height * 16;
            case TextureFormat.YUY2:
                return width * height * 2;
            // todo
            case TextureFormat.EAC_R:
            case TextureFormat.EAC_R_SIGNED:
            case TextureFormat.EAC_RG:
            case TextureFormat.EAC_RG_SIGNED:
            case TextureFormat.ETC_RGB4:
            case TextureFormat.ETC2_RGB4:
            case TextureFormat.ETC_RGB4_3DS:
            case TextureFormat.ETC_RGBA8_3DS:
            case TextureFormat.ETC2_RGBA1:
            case TextureFormat.ETC2_RGBA8:
                blockCountX = (width + 3) / 4;
                blockCountY = (height + 3) / 4;
                switch (format)
                {
                    case TextureFormat.EAC_R:
                    case TextureFormat.EAC_R_SIGNED:
                        return blockCountX * blockCountY * 8;
                    case TextureFormat.EAC_RG:
                    case TextureFormat.EAC_RG_SIGNED:
                        return blockCountX * blockCountY * 16;
                    case TextureFormat.ETC_RGB4:
                    case TextureFormat.ETC_RGB4_3DS:
                        return blockCountX * blockCountY * 8;
                    case TextureFormat.ETC2_RGB4:
                    case TextureFormat.ETC2_RGBA1:
                    case TextureFormat.ETC2_RGBA8:
                    case TextureFormat.ETC_RGBA8_3DS:
                        return blockCountX * blockCountY * 16;
                    default:
                        return 0; // can't happen
                }
            case TextureFormat.PVRTC_RGB2:
            case TextureFormat.PVRTC_RGBA2:
                blockCountX = (width + 7) / 8;
                blockCountY = (height + 3) / 4;
                goto pvrtc_all;
            case TextureFormat.PVRTC_RGB4:
            case TextureFormat.PVRTC_RGBA4:
                blockCountX = (width + 3) / 4;
                blockCountY = (height + 3) / 4;
                goto pvrtc_all;
            pvrtc_all:
                return blockCountX * blockCountY * 8;
            case TextureFormat.ASTC_RGB_4x4:
            case TextureFormat.ASTC_RGBA_4x4:
                blockCountX = (width + 3) / 4;
                blockCountY = (height + 3) / 4;
                goto astc_all;
            case TextureFormat.ASTC_RGB_5x5:
            case TextureFormat.ASTC_RGBA_5x5:
                blockCountX = (width + 4) / 5;
                blockCountY = (height + 4) / 5;
                goto astc_all;
            case TextureFormat.ASTC_RGB_6x6:
            case TextureFormat.ASTC_RGBA_6x6:
                blockCountX = (width + 5) / 6;
                blockCountY = (height + 5) / 6;
                goto astc_all;
            case TextureFormat.ASTC_RGB_8x8:
            case TextureFormat.ASTC_RGBA_8x8:
                blockCountX = (width + 7) / 8;
                blockCountY = (height + 7) / 8;
                goto astc_all;
            case TextureFormat.ASTC_RGB_10x10:
            case TextureFormat.ASTC_RGBA_10x10:
                blockCountX = (width + 9) / 10;
                blockCountY = (height + 9) / 10;
                goto astc_all;
            case TextureFormat.ASTC_RGB_12x12:
            case TextureFormat.ASTC_RGBA_12x12:
                blockCountX = (width + 11) / 12;
                blockCountY = (height + 11) / 12;
                goto astc_all;
            astc_all:
                return blockCountX * blockCountY * 16;
            case TextureFormat.DXT1:
            case TextureFormat.DXT5:
            case TextureFormat.BC4:
            case TextureFormat.BC5:
            case TextureFormat.BC6H:
            case TextureFormat.BC7:
                {
                    blockCountX = (width + 3) / 4;
                    blockCountY = (height + 3) / 4;
                    switch (format)
                    {
                        case TextureFormat.DXT1:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.DXT5:
                            return blockCountX * blockCountY * 16;
                        case TextureFormat.BC4:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.BC5:
                        case TextureFormat.BC6H:
                        case TextureFormat.BC7:
                            return blockCountX * blockCountY * 16;
                        default:
                            return 0; // can't happen
                    }
                }
            default:
                return width * height * 16; // don't know
        }
    }

    private static byte[] DecodeAssetRipperTex(byte[] data, int width, int height, TextureFormat format)
    {
        byte[] dest = TextureFile.DecodeManaged(data, format, width, height);

        for (int i = 0; i < dest.Length; i += 4)
        {
            byte temp = dest[i];
            dest[i] = dest[i + 2];
            dest[i + 2] = temp;
        }
        return dest;
    }

    public static byte[] Decode(byte[] data, int width, int height, TextureFormat format)
    {
        switch (format)
        {
            //crunch
            case TextureFormat.DXT1Crunched:
            case TextureFormat.DXT5Crunched:
            case TextureFormat.ETC_RGB4Crunched:
            case TextureFormat.ETC2_RGBA8Crunched:
                {
                    // 简化实现：Crunch 格式暂不支持，返回 null
                    Console.WriteLine($"警告: Crunch 格式 {format} 暂不支持");
                    return null;
                }
            //pvrtexlib - 使用 AssetRipper.TextureDecoder 解码
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
            case TextureFormat.RGBA32:
            case TextureFormat.RGB24:
            case TextureFormat.ARGB4444:
            case TextureFormat.RGBA4444:
            case TextureFormat.RGB565:
            case TextureFormat.Alpha8:
            case TextureFormat.R8:
            case TextureFormat.R16:
            case TextureFormat.RG16:
            case TextureFormat.RHalf:
            case TextureFormat.RGHalf:
            case TextureFormat.RGBAHalf:
            case TextureFormat.RFloat:
            case TextureFormat.RGFloat:
            case TextureFormat.RGBAFloat:
            case TextureFormat.YUY2:
            case TextureFormat.DXT1:
            case TextureFormat.DXT5:
            case TextureFormat.BC7:
            case TextureFormat.BC6H:
            case TextureFormat.BC4:
            case TextureFormat.BC5:
            case TextureFormat.RGB9e5Float:
            case TextureFormat.RGBA64:
            case TextureFormat.EAC_R:
            case TextureFormat.EAC_R_SIGNED:
            case TextureFormat.EAC_RG:
            case TextureFormat.EAC_RG_SIGNED:
            case TextureFormat.ETC_RGB4:
            case TextureFormat.ETC_RGB4_3DS:
            case TextureFormat.ETC_RGBA8_3DS:
            case TextureFormat.ETC2_RGB4:
            case TextureFormat.ETC2_RGBA1:
            case TextureFormat.ETC2_RGBA8:
                {
                    byte[] res = DecodeAssetRipperTex(data, width, height, format);
                    return res;
                }
            // PVRTC 和 ASTC 格式需要 PVRTexLib
            case TextureFormat.PVRTC_RGB2:
            case TextureFormat.PVRTC_RGBA2:
            case TextureFormat.PVRTC_RGB4:
            case TextureFormat.PVRTC_RGBA4:
            case TextureFormat.ASTC_RGB_4x4:
            case TextureFormat.ASTC_RGB_5x5:
            case TextureFormat.ASTC_RGB_6x6:
            case TextureFormat.ASTC_RGB_8x8:
            case TextureFormat.ASTC_RGB_10x10:
            case TextureFormat.ASTC_RGB_12x12:
            case TextureFormat.ASTC_RGBA_4x4:
            case TextureFormat.ASTC_RGBA_5x5:
            case TextureFormat.ASTC_RGBA_6x6:
            case TextureFormat.ASTC_RGBA_8x8:
            case TextureFormat.ASTC_RGBA_10x10:
            case TextureFormat.ASTC_RGBA_12x12:
                {
                    Console.WriteLine($"警告: 格式 {format} 需要 PVRTexLib，暂不支持");
                    return null;
                }
            default:
                return null;
        }
    }

    public static byte[] EncodeMip(byte[] data, int width, int height, TextureFormat format, int quality, int mips = 1)
    {
        switch (format)
        {
            // RGBA32 - 直接返回数据
            case TextureFormat.RGBA32:
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
                return data;

            // RGB24 - 去掉 alpha 通道
            case TextureFormat.RGB24:
                return EncodeRGB24(data, width, height);

            // Alpha8 - 只保留 alpha 通道
            case TextureFormat.Alpha8:
                return EncodeAlpha8(data, width, height);

            // DXT1/BC1 - 使用 BCnEncoder
            case TextureFormat.DXT1:
                return EncodeBCn(data, width, height, CompressionFormat.Bc1);

            // DXT5/BC3 - 使用 BCnEncoder  
            case TextureFormat.DXT5:
                return EncodeBCn(data, width, height, CompressionFormat.Bc3);

            // Crunch 格式 - 暂不支持
            case TextureFormat.DXT1Crunched:
            case TextureFormat.DXT5Crunched:
            case TextureFormat.ETC_RGB4Crunched:
            case TextureFormat.ETC2_RGBA8Crunched:
                Console.WriteLine($"警告: Crunch 格式 {format} 暂不支持");
                return null;

            // ETC_RGB4 - 使用 BC1/DXT1 编码替代（都是 4bpp 无 alpha 格式）
            case TextureFormat.ETC_RGB4:
                Console.WriteLine($"提示: ETC_RGB4 使用 DXT1/BC1 编码替代");
                return EncodeBCn(data, width, height, CompressionFormat.Bc1);

            // ETC2 格式 - 需要 PVRTexLib
            case TextureFormat.ETC2_RGB4:
            case TextureFormat.ETC2_RGBA1:
            case TextureFormat.ETC2_RGBA8:
            case TextureFormat.PVRTC_RGB2:
            case TextureFormat.PVRTC_RGBA2:
            case TextureFormat.PVRTC_RGB4:
            case TextureFormat.PVRTC_RGBA4:
            case TextureFormat.ASTC_RGB_4x4:
            case TextureFormat.ASTC_RGBA_4x4:
                Console.WriteLine($"警告: 格式 {format} 需要 PVRTexLib，暂不支持");
                return null;

            // 其他格式
            case TextureFormat.BC7:
            case TextureFormat.BC6H:
            case TextureFormat.BC4:
            case TextureFormat.BC5:
            case TextureFormat.RGB9e5Float:
            default:
                Console.WriteLine($"警告: 格式 {format} 暂不支持");
                return null;
        }
    }

    /// <summary>
    /// 使用 BCnEncoder 编码 BCn 格式
    /// </summary>
    private static byte[] EncodeBCn(byte[] rgbaData, int width, int height, CompressionFormat format)
    {
        try
        {
            // 加载图像
            using var image = Image.LoadPixelData<Rgba32>(rgbaData, width, height);
            
            // 创建编码器
            var encoder = new BcEncoder();
            encoder.OutputOptions.Format = format;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.GenerateMipMaps = false; // 不生成 mipmap，我们只编码当前级别
            
            // 编码 - EncodeToRawBytes 返回 byte[][]，每个元素是一个 mipmap 级别
            var encoded = encoder.EncodeToRawBytes(image);
            
            // 只返回第一个 mipmap 级别（主纹理）
            return encoded[0];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BCn 编码失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 编码为 RGB24（去掉 alpha 通道）
    /// </summary>
    private static byte[] EncodeRGB24(byte[] rgbaData, int width, int height)
    {
        int pixelCount = width * height;
        byte[] rgbData = new byte[pixelCount * 3];
        
        for (int i = 0; i < pixelCount; i++)
        {
            rgbData[i * 3] = rgbaData[i * 4];       // R
            rgbData[i * 3 + 1] = rgbaData[i * 4 + 1]; // G
            rgbData[i * 3 + 2] = rgbaData[i * 4 + 2]; // B
        }
        
        return rgbData;
    }

    /// <summary>
    /// 编码为 Alpha8（只保留 alpha 通道）
    /// </summary>
    private static byte[] EncodeAlpha8(byte[] rgbaData, int width, int height)
    {
        int pixelCount = width * height;
        byte[] alphaData = new byte[pixelCount];
        
        for (int i = 0; i < pixelCount; i++)
        {
            alphaData[i] = rgbaData[i * 4 + 3]; // Alpha
        }
        
        return alphaData;
    }

    public static byte[] Encode(SixLabors.ImageSharp.Image<Rgba32> image, int width, int height, TextureFormat format, int quality = 5, int mips = 1)
    {
        using MemoryStream rawDataStream = new MemoryStream();

        if (format == TextureFormat.DXT1Crunched || format == TextureFormat.DXT5Crunched ||
            format == TextureFormat.ETC_RGB4Crunched || format == TextureFormat.ETC2_RGBA8Crunched)
        {
            byte[] rawRgbaData = new byte[width * height * 4];
            image.CopyPixelDataTo(rawRgbaData);
            byte[] rawEncodedData = EncodeMip(rawRgbaData, width, height, format, quality, mips);
            if (rawEncodedData != null)
            {
                rawDataStream.Write(rawEncodedData);
            }
        }
        else
        {
            int curWidth = width;
            int curHeight = height;
            for (int i = 0; i < mips; i++)
            {
                byte[] rawRgbaData = new byte[curWidth * curHeight * 4];
                image.CopyPixelDataTo(rawRgbaData);
                byte[] rawEncodedData = EncodeMip(rawRgbaData, curWidth, curHeight, format, quality);
                if (rawEncodedData == null)
                {
                    return null;
                }
                rawDataStream.Write(rawEncodedData);

                if (i < mips - 1)
                {
                    curWidth >>= 1;
                    curHeight >>= 1;
                    image.Mutate(i => i.Resize(curWidth, curHeight));
                }
            }
        }

        return rawDataStream.ToArray();
    }
}
