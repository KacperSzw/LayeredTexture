using System;
using System.IO;
using System.Text;
using PsdSharp;
using PsdSharp.Images;
using PsdSharp.Images.DataConversion;
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
        /// <param name="texture">Loaded texture when successful.</param>
        /// <returns>True when the file exists, is supported, and decodes successfully.</returns>
        internal static bool TryLoad(string fullPath, out Texture2D texture)
        {
            texture = null;

            if (!File.Exists(fullPath) || !IsSupportedPath(fullPath))
                return false;

            var extension = Path.GetExtension(fullPath);

            if (string.Equals(extension, ".psd", StringComparison.OrdinalIgnoreCase))
            {
                texture = LoadPsd(fullPath);
            }
            else
            {
                var bytes = File.ReadAllBytes(fullPath);
                texture = string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase)
                    ? LoadTga(bytes)
                    : LoadPngJpg(bytes);
            }

            if (texture == null)
                return false;

            texture.name = Path.GetFileName(fullPath);
            texture.hideFlags = HideFlags.HideAndDontSave;
            return true;
        }

        static Texture2D LoadPngJpg(byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

            if (ImageConversion.LoadImage(texture, bytes, true))
                return texture;

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        static Texture2D LoadPsd(string fullPath)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var psd = PsdFile.Open(stream);

                if (psd.ImageData == null
                    || psd.Header.WidthInPixels == 0
                    || psd.Header.HeightInPixels == 0
                    || psd.Header.WidthInPixels > int.MaxValue
                    || psd.Header.HeightInPixels > int.MaxValue)
                    return null;

                var width = (int)psd.Header.WidthInPixels;
                var height = (int)psd.Header.HeightInPixels;
                var pixels = PixelDataConverter.GetInterleavedBuffer(psd.ImageData, ColorType.Rgba8888);
                var expectedLength = (long)width * height * 4;

                if (pixels == null || pixels.Length != expectedLength)
                    return null;

                FlipRows(pixels, width, height, 4);

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, true);
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static Texture2D LoadTga(byte[] bytes)
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

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
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
