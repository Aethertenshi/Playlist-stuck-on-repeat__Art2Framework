using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CoreGame
{
    public class LeaderboardEntry
    {
        public string PlayerName { get; set; } = "";
        public long Score { get; set; }
        public float Accuracy { get; set; }
        public int MaxCombo { get; set; }
        public string Mods { get; set; } = "NM";
        public DateTime Date { get; set; } = DateTime.Now;
    }

    public static class ScoreManager
    {
        private const string ScoresFileName = "scores.json";
        private static Dictionary<string, List<LeaderboardEntry>> _scores = new();

        static ScoreManager()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ScoresFileName))
                {
                    string json = File.ReadAllText(ScoresFileName);
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, List<LeaderboardEntry>>>(json);
                    if (deserialized != null)
                    {
                        _scores = deserialized;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScoreManager] Error loading scores: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_scores, options);
                File.WriteAllText(ScoresFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScoreManager] Error saving scores: {ex.Message}");
            }
        }

        public static void AddScore(string mapKey, string playerName, long score, float accuracy, int maxCombo, string mods)
        {
            if (string.IsNullOrEmpty(mapKey))
                return;

            if (!_scores.ContainsKey(mapKey))
            {
                _scores[mapKey] = new List<LeaderboardEntry>();
            }

            var entry = new LeaderboardEntry
            {
                PlayerName = playerName,
                Score = score,
                Accuracy = accuracy,
                MaxCombo = maxCombo,
                Mods = mods,
                Date = DateTime.Now
            };

            _scores[mapKey].Add(entry);
            // Sort by score descending
            _scores[mapKey] = _scores[mapKey].OrderByDescending(e => e.Score).ToList();
            Save();
        }

        public static List<LeaderboardEntry> GetScores(string mapKey, int count = 5)
        {
            if (string.IsNullOrEmpty(mapKey))
                return new List<LeaderboardEntry>();

            if (_scores.TryGetValue(mapKey, out var list))
            {
                return list.Take(count).ToList();
            }
            return new List<LeaderboardEntry>();
        }

        // Seeding mock rival scores based on map title & difficulty
        public static List<LeaderboardEntry> GetLeaderboard(string mapKey, string mapTitle, string difficultyName, int count = 5)
        {
            if (string.IsNullOrEmpty(mapKey))
                return new List<LeaderboardEntry>();

            // Get actual player scores
            var playerScores = GetScores(mapKey, count);

            // Generate deterministic mock scores
            int seed = (mapTitle + "_" + difficultyName).GetHashCode();
            var rand = new Random(seed);

            var mockScores = new List<LeaderboardEntry>();
            string[] rivalNames = { "Mrekk", "WubWoofWolf", "Cookiezi", "Rafis", "WhiteCat", "Vaxei", "Chocomint", "Ryuk", "BTMC", "Idke" };
            
            // Shuffle rival names
            var shuffledRivals = rivalNames.OrderBy(x => rand.Next()).ToList();

            // We generate 5 mock scores
            for (int i = 0; i < 5; i++)
            {
                string rival = shuffledRivals[i];
                float accuracy = 90.0f + (float)rand.NextDouble() * 10.0f; // 90% - 100%
                int maxCombo = rand.Next(150, 800);
                long score = (long)(maxCombo * 1000 + (accuracy * 5000) + rand.Next(50000, 500000));
                
                string[] modOptions = { "NM", "HD", "DT", "HDDT", "HT" };
                string mods = modOptions[rand.Next(modOptions.Length)];

                mockScores.Add(new LeaderboardEntry
                {
                    PlayerName = rival,
                    Score = score,
                    Accuracy = accuracy,
                    MaxCombo = maxCombo,
                    Mods = mods,
                    Date = DateTime.Now.AddDays(-rand.Next(1, 30))
                });
            }

            // Merge player and mock scores
            var allScores = new List<LeaderboardEntry>();
            allScores.AddRange(playerScores);
            allScores.AddRange(mockScores);

            // Sort by score descending and take the top 5
            return allScores.OrderByDescending(e => e.Score).Take(count).ToList();
        }
    }
}
