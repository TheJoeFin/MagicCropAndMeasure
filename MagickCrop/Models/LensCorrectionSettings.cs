using System;

namespace MagickCrop.Models;

public class LensCorrectionSettings
{
    // Barrel coefficients
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }

    // For future use: independent X/Y coefficients
    public double? Ax { get; set; }
    public double? Ay { get; set; }

    // Derived D coefficient: keep center scale
    public double D => 1.0 - A - B - C;

    public bool IsIdentity => Math.Abs(A) < 1e-9 && Math.Abs(B) < 1e-9 && Math.Abs(C) < 1e-9;

    public void Reset()
    {
        A = 0.0;
        B = 0.0;
        C = 0.0;
        Ax = null;
        Ay = null;
    }
}
