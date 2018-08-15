using System;
using System.IO;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

namespace RestServer.Util
{
    internal class ImageUtils
    {
        public static SKBitmap GuaranteeMaxSize(SKBitmap bitmap, int size)
        {
            (int width, int height) = GetWidthAndHeightForSize(bitmap, size);
            var resized = bitmap.Resize(new SKImageInfo(width, height), SKBitmapResizeMethod.Lanczos3);
            bitmap.Dispose();
            return resized;
        }

        private static (int, int) GetWidthAndHeightForSize(SKBitmap bitmap, int size)
        {
            int width, height;
            if (bitmap.Width > bitmap.Height)
            {
                width = size;
                height = bitmap.Height * size / bitmap.Width;
            }
            else
            {
                width = bitmap.Width * size / bitmap.Height;
                height = size;
            }
            return (width, height);
        }

        public static SKBitmap FromBytes(byte[] foto)
        {
            return SKBitmap.Decode(foto);
        }

        public static Stream ToStream(SKBitmap bitmap, SKEncodedImageFormat imageFormat, int quality)
        {
            using (var image = SKImage.FromBitmap(bitmap))
            {
                Stream output = new MemoryStream();
                image.Encode(imageFormat, quality).SaveTo(output);
                return output;
            }
        }

        public static Stream ToStream(SKBitmap bitmap, SKEncodedImageFormat imageFormat)
        {
            return ToStream(bitmap, imageFormat, 100);
        }

        public static Stream ToStream(SKBitmap bitmap)
        {
            return ToStream(bitmap, SKEncodedImageFormat.Jpeg);
        }
    }
}