using System;
using System.IO;
using RestServer.Exceptions;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

namespace RestServer.Util
{
    internal class ImageUtils
    {
        public static void ValidateMinSize(SKBitmap bitmap, int size)
        {
            if(bitmap.Width < size || bitmap.Height < size)
            {
                throw new ValidationException($"O tamanho da foto deve ser maior do que {size}!");
            }
        }

        public static SKBitmap GuaranteeMaxSize(SKBitmap bitmap, int size)
        {
            using (bitmap)
            {
                (int width, int height) = GetWidthAndHeightForMaxSize(bitmap, size);
                var resized = bitmap.Resize(new SKImageInfo(width, height), SKBitmapResizeMethod.Lanczos3);
                return resized;
            }
        }

        private static (int, int) GetWidthAndHeightForMaxSize(SKBitmap bitmap, int size)
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