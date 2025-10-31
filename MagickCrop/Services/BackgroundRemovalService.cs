using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace MagickCrop.Services;

public interface IBackgroundRemovalService
{
    bool IsAvailable { get; }
    System.Threading.Tasks.Task<BitmapSource?> RemoveBackgroundAsync(BitmapSource input);
}

/// <summary>
/// U2Net-style foreground segmentation using ONNX Runtime.
/// Prefers DirectML (GPU) when available, otherwise CPU. QNN (NPU) is not exposed in the .NET API yet.
/// </summary>
public sealed class BackgroundRemovalService : IBackgroundRemovalService, IDisposable
{
    private readonly string _modelPath;
    private InferenceSession? _session;
    public bool IsAvailable => _session != null;

    public BackgroundRemovalService()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _modelPath = Path.Combine(baseDir, "Assets", "Models", "u2net.onnx");
        TryInit();
    }

    private void TryInit()
    {
        try
        {
            if (!File.Exists(_modelPath)) return;
            SessionOptions opts = new SessionOptions();

            // Prefer DirectML for GPU
            try { opts.AppendExecutionProvider_DML(); } catch { }

            _session = new InferenceSession(_modelPath, opts);
        }
        catch
        {
            _session?.Dispose();
            _session = null;
        }
    }

    public async System.Threading.Tasks.Task<BitmapSource?> RemoveBackgroundAsync(BitmapSource input)
    {
        if (_session == null || input == null) return null;
        const int target = 320;
        return await System.Threading.Tasks.Task.Run(() => InferMaskAndComposite(input, target));
    }

    private BitmapSource InferMaskAndComposite(BitmapSource source, int size)
    {
        var scaled = ResizeTo(source, size, size);
        float[] chw = ToCHWFloat(scaled);
        var inputTensor = new DenseTensor<float>(chw, new int[] { 1, 3, size, size });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };
        using var results = _session!.Run(inputs);
        var output = results.FirstOrDefault(r => r.Name is "output" or "out" or "sigmoid" or "mask")
                     ?? results.First();
        var tensor = output.AsTensor<float>();
        int h = tensor.Dimensions[^2];
        int w = tensor.Dimensions[^1];
        float[] mask = new float[w * h];
        int idx = 0;
        foreach (float v in tensor)
            mask[idx++] = v;

        var maskBmp = FromGrayArray(mask, w, h);
        var maskUpscaled = ResizeTo(maskBmp, source.PixelWidth, source.PixelHeight);
        byte[] alpha = ExtractGray(maskUpscaled);

        return ApplyAlpha(source, alpha);
    }

    private static BitmapSource ResizeTo(BitmapSource src, int width, int height)
    {
        double sx = width / (double)src.PixelWidth;
        double sy = height / (double)src.PixelHeight;
        TransformedBitmap tb = new(src, new ScaleTransform(sx, sy));
        tb.Freeze();
        return tb;
    }

    private static float[] ToCHWFloat(BitmapSource src)
    {
        FormatConvertedBitmap fcb = new(src, PixelFormats.Bgra32, null, 0);
        fcb.Freeze();
        int w = fcb.PixelWidth;
        int h = fcb.PixelHeight;
        int stride = w * 4;
        byte[] pixels = new byte[stride * h];
        fcb.CopyPixels(pixels, stride, 0);
        float[] c = new float[3 * w * h];
        int plane = w * h;
        for (int y=0; y<h; y++)
        {
            int row = y * stride;
            for (int x=0; x<w; x++)
            {
                int p = row + x * 4;
                byte b = pixels[p + 0];
                byte g = pixels[p + 1];
                byte r = pixels[p + 2];
                int i = y * w + x;
                c[0 * plane + i] = r / 255f;
                c[1 * plane + i] = g / 255f;
                c[2 * plane + i] = b / 255f;
            }
        }
        return c;
    }

    private static BitmapSource FromGrayArray(float[] values, int width, int height)
    {
        byte[] bytes = new byte[width * height];
        for (int i = 0; i < bytes.Length; i++)
        {
            float v = Math.Clamp(values[i], 0f, 1f);
            bytes[i] = (byte)(v * 255f);
        }
        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, bytes, width);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] ExtractGray(BitmapSource gray)
    {
        if (gray.Format != PixelFormats.Gray8)
        {
            gray = new FormatConvertedBitmap(gray, PixelFormats.Gray8, null, 0);
        }
        int stride = gray.PixelWidth;
        byte[] data = new byte[stride * gray.PixelHeight];
        gray.CopyPixels(data, stride, 0);
        return data;
    }

    private static BitmapSource ApplyAlpha(BitmapSource rgb, byte[] alpha)
    {
        if (rgb.Format != PixelFormats.Bgra32)
            rgb = new FormatConvertedBitmap(rgb, PixelFormats.Bgra32, null, 0);
        int w = rgb.PixelWidth;
        int h = rgb.PixelHeight;
        int stride = w * 4;
        byte[] pixels = new byte[stride * h];
        rgb.CopyPixels(pixels, stride, 0);
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            int arow = y * w;
            for (int x = 0; x < w; x++)
            {
                int p = row + x * 4;
                pixels[p + 3] = alpha[arow + x];
            }
        }
        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
