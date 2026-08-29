using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Imaging;
using SkiaSharp;

namespace MuseumSystem.Integration.Tests.Photography;

internal static class PhotographyIntegrationTestImages
{
    public static byte[] Jpeg(int width = 800, int height = 600) =>
        Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] Png(int width = 800, int height = 600) =>
        Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Gif() =>
        Convert.FromBase64String("R0lGODlhAQABAAAAACwAAAAAAQABAAA=");

    public static MemoryStream Stream(byte[] bytes) => new(bytes, writable: false);

    public static PhotographyUploadFileInput UploadFile(int ordinal, string filename, byte[] bytes) =>
        new(ordinal, filename, Stream(bytes), bytes.LongLength);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(24, 82, 108));
        using var paint = new SKPaint { Color = new SKColor(236, 190, 74), IsAntialias = true };
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 4f, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
