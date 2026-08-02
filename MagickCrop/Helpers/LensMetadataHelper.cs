using ImageMagick;
using System;

namespace MagickCrop.Helpers;

public record LensMetadata(
    string? CameraMake,
    string? CameraModel,
    string? LensMake,
    string? LensModel,
    double? FocalLength,
    double? FNumber,
    int? Orientation
);

public static class LensMetadataHelper
{
    public static LensMetadata? Read(string imagePath)
    {
        try
        {
            using MagickImage img = new(imagePath);
            var exif = img.GetExifProfile();
            if (exif is null)
                return null;

            string? make = exif.GetValue(ExifTag.Make)?.ToString();
            string? model = exif.GetValue(ExifTag.Model)?.ToString();
            string? lensModel = exif.GetValue(ExifTag.LensModel)?.ToString();
            string? lensMake = exif.GetValue(ExifTag.LensMake)?.ToString();

            double? focal = null;
            var fl = exif.GetValue(ExifTag.FocalLength)?.ToString();
            if (fl is not null && double.TryParse(fl, out double fld)) focal = fld;

            double? fnum = null;
            var fn = exif.GetValue(ExifTag.FNumber)?.ToString();
            if (fn is not null && double.TryParse(fn, out double fnd)) fnum = fnd;

            int? orientation = null;
            var orient = exif.GetValue(ExifTag.Orientation)?.ToString();
            if (orient is not null && int.TryParse(orient, out int o)) orientation = o;

            return new LensMetadata(make, model, lensMake, lensModel, focal, fnum, orientation);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
