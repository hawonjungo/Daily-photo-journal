using SkiaSharp;

namespace Relifes.Api.Images;

public record ProcessedImage(byte[] Full, byte[] Thumbnail);

public class InvalidImageException(string message) : Exception(message);

public static class ImageProcessor
{
    public const string ContentType = "image/webp";
    public const int FullMaxSize = 1600;
    public const int ThumbnailMaxSize = 400;

    private const int WebpQuality = 80;

    // Decoding takes 4 bytes per pixel, so this also caps memory use at about 256 MB per upload.
    private const long MaxPixels = 64_000_000;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// Only decoded pixels are written to the output, so EXIF, GPS and every other kind of metadata
    /// from the source never reach the stored files.
    /// </summary>
    public static ProcessedImage Process(Stream source)
    {
        using var data = SKData.Create(source);
        using var codec = data is null ? null : SKCodec.Create(data);

        if (codec?.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            throw new InvalidImageException("File is not a valid JPEG, PNG or WebP image.");
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new InvalidImageException("Image has too many pixels (limit is 64 megapixels).");
        }

        // Requesting sRGB converts wide-gamut photos, whose colour profile would otherwise be lost.
        var info = new SKImageInfo(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());

        using var decoded = new SKBitmap(info);
        if (codec.GetPixels(info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidImageException("File is not a valid JPEG, PNG or WebP image.");
        }

        using var rotated = codec.EncodedOrigin == SKEncodedOrigin.TopLeft ? null : Orient(decoded, codec.EncodedOrigin);
        var upright = rotated ?? decoded;

        return new ProcessedImage(
            EncodeShrunk(upright, FullMaxSize),
            EncodeShrunk(upright, ThumbnailMaxSize));
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var (w, h) = (source.Width, source.Height);
        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var result = new SKBitmap(source.Info.WithSize(swapsSides ? h : w, swapsSides ? w : h));
        using var canvas = new SKCanvas(result);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(w, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(w, h);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, h);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(h, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(h, w);
                canvas.RotateDegrees(-90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, w);
                canvas.RotateDegrees(-90);
                break;
        }

        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    // Images smaller than maxSize keep their size: enlarging would only waste storage.
    private static byte[] EncodeShrunk(SKBitmap source, int maxSize)
    {
        var scale = (double)maxSize / Math.Max(source.Width, source.Height);
        if (scale >= 1)
        {
            return Encode(source);
        }

        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = new SKBitmap(source.Info.WithSize(width, height));
        if (!source.ScalePixels(resized, Sampling))
        {
            throw new InvalidOperationException("Could not resize image.");
        }

        return Encode(resized);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        using var encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, WebpQuality))
            ?? throw new InvalidOperationException("Could not encode image.");
        return encoded.ToArray();
    }
}
