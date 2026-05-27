using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using OsuLib;
using OppaiSharp;

namespace CoreGame
{
    public static class StarRatingCache
    {
        private const string CacheFileName = "songs_star_cache.json";
        private static readonly Dictionary<string, float> _cache = new();

        public static void Load()
        {
            try
            {
                if (File.Exists(CacheFileName))
                {
                    string json = File.ReadAllText(CacheFileName);
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, float>>(json);
                    if (deserialized != null)
                    {
                        foreach (var kvp in deserialized)
                        {
                            _cache[kvp.Key] = kvp.Value;
                        }
                        Console.WriteLine($"[StarRatingCache] Cache loaded successfully. Found {_cache.Count} entries.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarRatingCache] Error loading cache: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_cache, options);
                File.WriteAllText(CacheFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarRatingCache] Error saving cache: {ex.Message}");
            }
        }

        public static float GetRealStarRating(OsuBeatmap? bm)
        {
            if (bm == null)
                return 0f;

            // Safety check
            if (string.IsNullOrEmpty(bm.FilePath) || !File.Exists(bm.FilePath))
                return 0f;

            // Check cache first
            if (_cache.TryGetValue(bm.FilePath, out float cachedRating))
                return cachedRating;

            try
            {
                // 1. Open a StreamReader directly to your local .osu file
                using (var reader = new StreamReader(bm.FilePath))
                {
                    // 2. Let OppaiSharp parse the file for physics calculations
                    var oppaiBeatmap = OppaiSharp.Beatmap.Read(reader);

                    // 3. Calculate the difficulty (using NoMod for the base Star Rating)
                    var diff = new DiffCalc().Calc(oppaiBeatmap, Mods.NoMod);

                    float rating = (float)diff.Total;

                    // Cache and save
                    _cache[bm.FilePath] = rating;
                    Save();

                    return rating;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarRatingCache] Error calculating star rating for {bm.Title}: {ex.Message}");
                return 0f;
            }
        }
    }
}
