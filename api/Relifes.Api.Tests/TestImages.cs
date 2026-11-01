using System.Text;
using SkiaSharp;

namespace Relifes.Api.Tests;

public static class TestImages
{
    public const string ExifMarker = "SECRET-CAMERA";

    public static readonly SKColor TopLeft = SKColors.Red;
    public static readonly SKColor TopRight = SKColors.Lime;
    public static readonly SKColor BottomLeft = SKColors.Blue;
    public static readonly SKColor BottomRight = SKColors.Yellow;

    /// <summary>JPEG with a different colour in each quadrant, so any rotation or flip is detectable.</summary>
    public static byte[] Jpeg(int width, int height, ushort? exifOrientation = null)
    {
        using var bitmap = Quadrants(width, height);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95);
        var jpeg = encoded.ToArray();
        return exifOrientation is { } orientation ? InsertExif(jpeg, orientation) : jpeg;
    }

    public static byte[] Png(int width, int height)
    {
        using var bitmap = Quadrants(width, height);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    /// <summary>JPEG whose header claims 10000x10000 pixels, without the memory cost of creating one.</summary>
    public static byte[] JpegClaimingHugeSize()
    {
        var jpeg = Jpeg(16, 16);
        for (var i = 2; i < jpeg.Length - 8; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xC0)
            {
                jpeg[i + 5] = jpeg[i + 7] = 10000 >> 8;
                jpeg[i + 6] = jpeg[i + 8] = 10000 & 0xFF;
                return jpeg;
            }
        }

        throw new InvalidOperationException("JPEG start-of-frame marker not found.");
    }

    private static SKBitmap Quadrants(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();

        void Fill(SKColor color, int left, int top)
        {
            paint.Color = color;
            canvas.DrawRect(left, top, width / 2f, height / 2f, paint);
        }

        Fill(TopLeft, 0, 0);
        Fill(TopRight, width / 2, 0);
        Fill(BottomLeft, 0, height / 2);
        Fill(BottomRight, width / 2, height / 2);
        return bitmap;
    }

    private static byte[] InsertExif(byte[] jpeg, ushort orientation)
    {
        var make = Encoding.ASCII.GetBytes(ExifMarker + "\0");
        const int ifdEntries = 2;
        var makeOffset = 8 + 2 + ifdEntries * 12 + 4;

        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, ifdEntries };
        // Make: ASCII string stored after the directory.
        tiff.AddRange([0x01, 0x0F, 0, 2, 0, 0, 0, (byte)make.Length, 0, 0, 0, (byte)makeOffset]);
        // Orientation: a single SHORT stored inline.
        tiff.AddRange([0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, (byte)orientation, 0, 0]);
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(make);

        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var segmentLength = payload.Length + 2;

        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)segmentLength, .. payload, .. jpeg[2..]];
    }
}
