using System.Text;
using Relifes.Api.Images;
using SkiaSharp;

namespace Relifes.Api.Tests;

public class ImageProcessorTests
{
    private static ProcessedImage Process(byte[] bytes) => ImageProcessor.Process(new MemoryStream(bytes));

    private static SKBitmap Decode(byte[] webp)
    {
        using var codec = SKCodec.Create(new MemoryStream(webp));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        return SKBitmap.Decode(webp);
    }

    private static SKColor ClosestQuadrantColor(SKColor actual)
    {
        SKColor[] colors = [TestImages.TopLeft, TestImages.TopRight, TestImages.BottomLeft, TestImages.BottomRight];
        return colors.MinBy(c =>
            Math.Pow(c.Red - actual.Red, 2) + Math.Pow(c.Green - actual.Green, 2) + Math.Pow(c.Blue - actual.Blue, 2));
    }

    [Fact]
    public void Landscape_image_is_limited_to_max_sizes_keeping_aspect_ratio()
    {
        var result = Process(TestImages.Jpeg(3000, 2000));

        using var full = Decode(result.Full);
        using var thumbnail = Decode(result.Thumbnail);
        Assert.Equal((1600, 1067), (full.Width, full.Height));
        Assert.Equal((400, 267), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void Small_image_is_not_enlarged()
    {
        var result = Process(TestImages.Png(100, 50));

        using var full = Decode(result.Full);
        using var thumbnail = Decode(result.Thumbnail);
        Assert.Equal((100, 50), (full.Width, full.Height));
        Assert.Equal((100, 50), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void Exif_is_removed_from_both_outputs()
    {
        var source = TestImages.Jpeg(800, 600, exifOrientation: 1);
        Assert.Contains(TestImages.ExifMarker, Encoding.ASCII.GetString(source));

        var result = Process(source);

        foreach (var output in new[] { result.Full, result.Thumbnail })
        {
            var text = Encoding.ASCII.GetString(output);
            Assert.DoesNotContain(TestImages.ExifMarker, text);
            Assert.DoesNotContain("Exif", text);
            Assert.DoesNotContain("EXIF", text);
        }
    }

    // Expected corner colours per EXIF orientation, given source corners TL=R, TR=G, BL=B, BR=Y.
    [Theory]
    [InlineData(1, "RGBY", false)]
    [InlineData(2, "GRYB", false)]
    [InlineData(3, "YBGR", false)]
    [InlineData(4, "BYRG", false)]
    [InlineData(5, "RBGY", true)]
    [InlineData(6, "BRYG", true)]
    [InlineData(7, "YGBR", true)]
    [InlineData(8, "GYRB", true)]
    public void Exif_orientation_is_applied_to_the_pixels(ushort orientation, string expectedCorners, bool swapsSides)
    {
        var result = Process(TestImages.Jpeg(400, 200, orientation));

        using var full = Decode(result.Full);
        Assert.Equal(swapsSides ? (200, 400) : (400, 200), (full.Width, full.Height));

        var (left, right) = (full.Width / 4, full.Width * 3 / 4);
        var (top, bottom) = (full.Height / 4, full.Height * 3 / 4);
        var names = new Dictionary<SKColor, char>
        {
            [TestImages.TopLeft] = 'R',
            [TestImages.TopRight] = 'G',
            [TestImages.BottomLeft] = 'B',
            [TestImages.BottomRight] = 'Y',
        };
        var corners = new[]
        {
            full.GetPixel(left, top), full.GetPixel(right, top), full.GetPixel(left, bottom), full.GetPixel(right, bottom),
        };

        Assert.Equal(expectedCorners, string.Concat(corners.Select(c => names[ClosestQuadrantColor(c)])));
    }

    [Fact]
    public void Non_image_is_rejected()
    {
        Assert.Throws<InvalidImageException>(() => Process(Encoding.UTF8.GetBytes("this is not an image at all")));
    }

    [Fact]
    public void Unsupported_format_is_rejected()
    {
        // Minimal GIF: header, 1x1 screen, and terminator.
        byte[] gif = [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B];

        Assert.Throws<InvalidImageException>(() => Process(gif));
    }

    [Fact]
    public void Truncated_image_is_rejected()
    {
        var jpeg = TestImages.Jpeg(800, 600);

        Assert.Throws<InvalidImageException>(() => Process(jpeg[..(jpeg.Length / 2)]));
    }

    [Fact]
    public void Image_over_the_pixel_limit_is_rejected_before_decoding()
    {
        var exception = Assert.Throws<InvalidImageException>(() => Process(TestImages.JpegClaimingHugeSize()));

        Assert.Contains("too many pixels", exception.Message);
    }
}
