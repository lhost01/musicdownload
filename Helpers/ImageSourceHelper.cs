using Avalonia.Media.Imaging;

namespace Musicbox.Helpers;

public static class ImageSourceHelper
{
    public static Bitmap? LoadBitmap(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
