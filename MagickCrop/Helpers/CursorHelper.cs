using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagickCrop.Helpers;

/// <summary>
/// Builds the closed-hand ("grabbing") cursor shown while the canvas is being panned. WPF ships no
/// such cursor and the app carries no binary assets, so it is drawn at runtime and packed into an
/// in-memory .cur file.
/// </summary>
public static class CursorHelper
{
    private const int CursorSize = 32;
    private const int Hotspot = CursorSize / 2;

    private static Cursor? grabbingCursor;

    /// <summary>Closed hand — shown while the canvas is being dragged.</summary>
    public static Cursor Grabbing => grabbingCursor ??= Create() ?? Cursors.SizeAll;

    private static Cursor? Create()
    {
        try
        {
            byte[] bgra = RenderGrabbingHand();
            using MemoryStream stream = new();
            WriteCursorFile(stream, bgra);
            stream.Position = 0;
            return new Cursor(stream);
        }
        catch (Exception)
        {
            // Any failure here is cosmetic — the caller falls back to a stock cursor.
            return null;
        }
    }

    /// <summary>
    /// Draws a closed-hand glyph and returns it as top-down premultiplied BGRA rows.
    /// </summary>
    private static byte[] RenderGrabbingHand()
    {
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            Pen outline = new(Brushes.Black, 1.4);
            outline.Freeze();

            // Palm
            context.DrawRoundedRectangle(Brushes.White, outline, new Rect(9, 15, 15, 12), 4, 4);

            // Fingers, curled down into the palm
            for (int i = 0; i < 4; i++)
            {
                double x = 10 + (i * 3.6);
                context.DrawRoundedRectangle(Brushes.White, outline, new Rect(x, 11, 3, 5), 1.5, 1.5);
            }

            // Thumb
            context.DrawRoundedRectangle(Brushes.White, outline, new Rect(6, 17, 5, 3), 1.5, 1.5);
        }

        RenderTargetBitmap bitmap = new(CursorSize, CursorSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        int stride = CursorSize * 4;
        byte[] pixels = new byte[stride * CursorSize];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    /// <summary>
    /// Packs 32-bpp BGRA pixels into a classic (non-PNG) .cur: ICONDIR + ICONDIRENTRY +
    /// BITMAPINFOHEADER + bottom-up XOR rows + an all-zero AND mask.
    /// </summary>
    private static void WriteCursorFile(Stream stream, byte[] bgraTopDown)
    {
        int stride = CursorSize * 4;
        int maskStride = ((CursorSize + 31) / 32) * 4; // AND mask rows are DWORD aligned
        int xorSize = stride * CursorSize;
        int andSize = maskStride * CursorSize;
        int imageSize = 40 + xorSize + andSize;

        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICONDIR
        writer.Write((ushort)0);   // reserved
        writer.Write((ushort)2);   // 2 = cursor
        writer.Write((ushort)1);   // one image

        // ICONDIRENTRY — for cursors the "planes"/"bitCount" fields carry the hotspot
        writer.Write((byte)CursorSize);
        writer.Write((byte)CursorSize);
        writer.Write((byte)0);     // colour count (0 = >=256)
        writer.Write((byte)0);     // reserved
        writer.Write((ushort)Hotspot);
        writer.Write((ushort)Hotspot);
        writer.Write(imageSize);
        writer.Write(22);          // offset of the image data

        // BITMAPINFOHEADER — height is doubled to cover the XOR and AND bitmaps
        writer.Write(40);
        writer.Write(CursorSize);
        writer.Write(CursorSize * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);           // BI_RGB
        writer.Write(xorSize + andSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        // XOR bitmap, bottom-up
        for (int y = CursorSize - 1; y >= 0; y--)
            writer.Write(bgraTopDown, y * stride, stride);

        // AND mask — unused for 32-bpp cursors, but must be present
        writer.Write(new byte[andSize]);
    }
}
