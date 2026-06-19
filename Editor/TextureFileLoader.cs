using System;
using System.IO;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Loads editor-only texture files used by file-backed texture sources.
    /// </summary>
    static class TextureFileLoader
    {
        internal static bool IsSupportedPath(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".psd", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads a supported image file as a hidden, non-saved Texture2D owned by the caller.
        /// </summary>
        /// <param name="fullPath">Absolute filesystem path to the image file.</param>
        /// <param name="srgb">Whether RGB should be sampled through sRGB decoding.</param>
        /// <param name="texture">Loaded texture when successful.</param>
        /// <returns>True when the file exists, is supported, and decodes successfully.</returns>
        internal static bool TryLoad(string fullPath, bool srgb, out Texture2D texture)
        {
            texture = null;

            if (!File.Exists(fullPath) || !IsSupportedPath(fullPath))
                return false;

            var extension = Path.GetExtension(fullPath);

            if (string.Equals(extension, ".psd", StringComparison.OrdinalIgnoreCase))
            {
                texture = LoadPsd(fullPath, srgb);
            }
            else
            {
                var bytes = File.ReadAllBytes(fullPath);
                texture = string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase)
                    ? LoadTga(bytes, srgb)
                    : LoadPngJpg(bytes, srgb);
            }

            if (texture == null)
                return false;

            texture.name = Path.GetFileName(fullPath);
            texture.hideFlags = HideFlags.HideAndDontSave;
            return true;
        }

        static Texture2D LoadPngJpg(byte[] bytes, bool srgb)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, !srgb);

            if (ImageConversion.LoadImage(texture, bytes, true))
                return texture;

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        static Texture2D LoadPsd(string fullPath, bool srgb)
        {
            try
            {
                using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);

                if (reader.ReadByte() != '8'
                    || reader.ReadByte() != 'B'
                    || reader.ReadByte() != 'P'
                    || reader.ReadByte() != 'S')
                    return null;

                if (ReadUInt16(reader) != 1)
                    return null;

                stream.Position += 6;
                var channels = ReadUInt16(reader);
                var height = ReadUInt32(reader);
                var width = ReadUInt32(reader);
                var depth = ReadUInt16(reader);
                var colorMode = ReadUInt16(reader);

                if (width == 0
                    || height == 0
                    || width > int.MaxValue
                    || height > int.MaxValue
                    || depth != 8
                    || !CanDecodePsdColorMode(colorMode, channels))
                    return null;

                SkipBlock(reader);
                SkipBlock(reader);
                SkipBlock(reader);

                var compression = ReadUInt16(reader);
                var channelData = ReadPsdChannels(reader, (int)width, (int)height, channels, compression);

                if (channelData == null)
                    return null;

                var pixels = InterleavePsdPixels(channelData, (int)width, (int)height, colorMode);
                FlipRows(pixels, (int)width, (int)height, 4);

                var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, !srgb);
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, true);
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool CanDecodePsdColorMode(int colorMode, int channels) =>
            colorMode switch
            {
                1 => channels is 1 or 2,
                3 => channels is 3 or 4,
                _ => false
            };

        static byte[][] ReadPsdChannels(BinaryReader reader, int width, int height, int channels, int compression)
        {
            var pixelCount = width * height;
            var channelData = new byte[channels][];

            for (var i = 0; i < channels; i++)
                channelData[i] = new byte[pixelCount];

            switch (compression)
            {
                case 0:
                    for (var channel = 0; channel < channels; channel++)
                    {
                        if (reader.Read(channelData[channel], 0, pixelCount) != pixelCount)
                            return null;
                    }

                    return channelData;
                case 1:
                    return ReadRlePsdChannels(reader, width, height, channelData);
                default:
                    return null;
            }
        }

        static byte[][] ReadRlePsdChannels(BinaryReader reader, int width, int height, byte[][] channelData)
        {
            var rowLengths = new int[channelData.Length * height];

            for (var i = 0; i < rowLengths.Length; i++)
                rowLengths[i] = ReadUInt16(reader);

            for (var channel = 0; channel < channelData.Length; channel++)
            {
                for (var y = 0; y < height; y++)
                {
                    var row = ReadPackBitsRow(reader, rowLengths[channel * height + y], width);

                    if (row == null)
                        return null;

                    Buffer.BlockCopy(row, 0, channelData[channel], y * width, width);
                }
            }

            return channelData;
        }

        static byte[] ReadPackBitsRow(BinaryReader reader, int byteCount, int width)
        {
            var end = reader.BaseStream.Position + byteCount;
            var row = new byte[width];
            var offset = 0;

            while (reader.BaseStream.Position < end && offset < width)
            {
                var header = unchecked((sbyte)reader.ReadByte());

                if (header >= 0)
                {
                    var count = header + 1;

                    if (offset + count > width || reader.Read(row, offset, count) != count)
                        return null;

                    offset += count;
                    continue;
                }

                if (header == -128)
                    continue;

                var repeat = 1 - header;

                if (offset + repeat > width || reader.BaseStream.Position >= end)
                    return null;

                var value = reader.ReadByte();

                for (var i = 0; i < repeat; i++)
                    row[offset++] = value;
            }

            reader.BaseStream.Position = end;
            return offset == width ? row : null;
        }

        static byte[] InterleavePsdPixels(byte[][] channelData, int width, int height, int colorMode)
        {
            var pixelCount = width * height;
            var pixels = new byte[pixelCount * 4];

            for (var i = 0; i < pixelCount; i++)
            {
                var offset = i * 4;

                if (colorMode == 1)
                {
                    var value = channelData[0][i];
                    pixels[offset] = value;
                    pixels[offset + 1] = value;
                    pixels[offset + 2] = value;
                    pixels[offset + 3] = channelData.Length > 1 ? channelData[1][i] : (byte)255;
                    continue;
                }

                pixels[offset] = channelData[0][i];
                pixels[offset + 1] = channelData[1][i];
                pixels[offset + 2] = channelData[2][i];
                pixels[offset + 3] = channelData.Length > 3 ? channelData[3][i] : (byte)255;
            }

            return pixels;
        }

        static void SkipBlock(BinaryReader reader)
        {
            var length = ReadUInt32(reader);
            reader.BaseStream.Position += length;
        }

        static ushort ReadUInt16(BinaryReader reader)
        {
            var high = reader.ReadByte();
            var low = reader.ReadByte();
            return (ushort)((high << 8) | low);
        }

        static uint ReadUInt32(BinaryReader reader)
        {
            var a = reader.ReadByte();
            var b = reader.ReadByte();
            var c = reader.ReadByte();
            var d = reader.ReadByte();
            return (uint)((a << 24) | (b << 16) | (c << 8) | d);
        }

        static Texture2D LoadTga(byte[] bytes, bool srgb)
        {
            if (bytes.Length < 18 || bytes[1] != 0)
                return null;

            var imageType = bytes[2];
            var width = bytes[12] | (bytes[13] << 8);
            var height = bytes[14] | (bytes[15] << 8);
            var bitDepth = bytes[16];
            var descriptor = bytes[17];

            if (width <= 0 || height <= 0 || bitDepth is not (8 or 24 or 32))
                return null;

            if (imageType is not (2 or 3 or 10 or 11))
                return null;

            var offset = 18 + bytes[0];

            if (offset >= bytes.Length)
                return null;

            var pixels = new Color32[width * height];
            var topOrigin = (descriptor & 32) != 0;
            var pixelIndex = 0;

            while (pixelIndex < pixels.Length && offset < bytes.Length)
            {
                if (imageType is 10 or 11)
                {
                    var packet = bytes[offset++];
                    var count = (packet & 127) + 1;

                    if ((packet & 128) != 0)
                    {
                        if (!TryReadPixel(bytes, ref offset, bitDepth, out var color))
                            return null;

                        for (var i = 0; i < count && pixelIndex < pixels.Length; i++)
                            WritePixel(pixels, width, height, topOrigin, pixelIndex++, color);
                    }
                    else
                    {
                        for (var i = 0; i < count && pixelIndex < pixels.Length; i++)
                        {
                            if (!TryReadPixel(bytes, ref offset, bitDepth, out var color))
                                return null;

                            WritePixel(pixels, width, height, topOrigin, pixelIndex++, color);
                        }
                    }
                }
                else
                {
                    if (!TryReadPixel(bytes, ref offset, bitDepth, out var color))
                        return null;

                    WritePixel(pixels, width, height, topOrigin, pixelIndex++, color);
                }
            }

            if (pixelIndex != pixels.Length)
                return null;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool TryReadPixel(byte[] bytes, ref int offset, int bitDepth, out Color32 color)
        {
            color = default;

            if (offset + bitDepth / 8 > bytes.Length)
                return false;

            if (bitDepth == 8)
            {
                var value = bytes[offset++];
                color = new Color32(value, value, value, 255);
                return true;
            }

            var b = bytes[offset++];
            var g = bytes[offset++];
            var r = bytes[offset++];
            var a = bitDepth == 32 ? bytes[offset++] : (byte)255;
            color = new Color32(r, g, b, a);
            return true;
        }

        static void WritePixel(Color32[] pixels, int width, int height, bool topOrigin, int index, Color32 color)
        {
            var x = index % width;
            var y = index / width;

            if (!topOrigin)
                y = height - 1 - y;

            pixels[y * width + x] = color;
        }

        static void FlipRows(byte[] pixels, int width, int height, int bytesPerPixel)
        {
            var rowSize = width * bytesPerPixel;
            var row = new byte[rowSize];
            var top = 0;
            var bottom = (height - 1) * rowSize;

            while (top < bottom)
            {
                Buffer.BlockCopy(pixels, top, row, 0, rowSize);
                Buffer.BlockCopy(pixels, bottom, pixels, top, rowSize);
                Buffer.BlockCopy(row, 0, pixels, bottom, rowSize);
                top += rowSize;
                bottom -= rowSize;
            }
        }
    }
}
