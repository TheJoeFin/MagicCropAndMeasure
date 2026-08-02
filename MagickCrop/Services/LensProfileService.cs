using MagickCrop.Models;
using MagickCrop.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace MagickCrop.Services;

public class LensProfileEntry
{
    public string? Key { get; set; }
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }

    [JsonIgnore]
    public bool IsUserDefined { get; set; }

    public override string ToString() => Key ?? string.Empty;
}

public static class LensProfileService
{
    private static List<LensProfileEntry>? _profiles;

    private static string UserProfilesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MagickCrop",
        "LensProfiles.user.json");

    private static void EnsureLoaded()
    {
        if (_profiles is not null) return;

        List<LensProfileEntry> profiles = [];

        try
        {
            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            string jsonPath = Path.Combine(baseDir, "Resources", "LensProfiles.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                profiles.AddRange(JsonSerializer.Deserialize<List<LensProfileEntry>>(json) ?? []);
            }
        }
        catch (Exception)
        {
            // A missing or malformed built-in table should not block user profiles.
        }

        try
        {
            if (File.Exists(UserProfilesPath))
            {
                string json = File.ReadAllText(UserProfilesPath);
                foreach (LensProfileEntry entry in JsonSerializer.Deserialize<List<LensProfileEntry>>(json) ?? [])
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                    // User entries win over a built-in profile with the same key.
                    profiles.RemoveAll(p => string.Equals(p.Key, entry.Key, StringComparison.OrdinalIgnoreCase));
                    entry.IsUserDefined = true;
                    profiles.Add(entry);
                }
            }
        }
        catch (Exception)
        {
            // Ignore corrupt user profile files.
        }

        _profiles = profiles;
    }

    public static IReadOnlyList<LensProfileEntry> GetProfiles()
    {
        EnsureLoaded();
        return _profiles!;
    }

    public static LensProfileEntry? Save(string key, double a, double b, double c)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        EnsureLoaded();

        key = key.Trim();
        LensProfileEntry entry = new() { Key = key, A = a, B = b, C = c, IsUserDefined = true };

        _profiles!.RemoveAll(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        _profiles.Add(entry);

        try
        {
            string? directory = Path.GetDirectoryName(UserProfilesPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            List<LensProfileEntry> userProfiles = [.. _profiles.Where(p => p.IsUserDefined)];
            string json = JsonSerializer.Serialize(userProfiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UserProfilesPath, json);
        }
        catch (Exception)
        {
            return null;
        }

        return entry;
    }

    public static LensCorrectionSettings? Lookup(LensMetadata? meta)
    {
        if (meta is null) return null;
        EnsureLoaded();

        string combined = string.Join(" ", new[] { meta.CameraMake, meta.CameraModel, meta.LensMake, meta.LensModel }).Trim();
        if (string.IsNullOrEmpty(combined)) return null;

        foreach (var p in _profiles!)
        {
            if (string.IsNullOrEmpty(p.Key)) continue;
            if (combined.IndexOf(p.Key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new LensCorrectionSettings { A = p.A, B = p.B, C = p.C };
            }
        }

        return null;
    }
}
