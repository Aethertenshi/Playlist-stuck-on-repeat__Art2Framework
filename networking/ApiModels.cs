using System.Text.Json.Serialization;

namespace CoreGame
{
    // ─── Auth Request / Response Models ───────────────────────────────────────

    public class LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }

    public class RegisterRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";
    }

    public class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class RegisterResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";
    }

    public class RefreshResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    // ─── Score Request / Response Models ──────────────────────────────────────

    public class ScoreSubmitRequest
    {
        [JsonPropertyName("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("max_combo")]
        public int MaxCombo { get; set; }

        [JsonPropertyName("hits_300")]
        public int Hits300 { get; set; }

        [JsonPropertyName("hits_200")]
        public int Hits200 { get; set; }

        [JsonPropertyName("hits_50")]
        public int Hits50 { get; set; }

        [JsonPropertyName("misses")]
        public int Misses { get; set; }

        [JsonPropertyName("gameplay_mode")]
        public string GameplayMode { get; set; } = "taiko";

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = "Unknown";
    }

    public class ScoreSubmitResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("score_id")]
        public long ScoreId { get; set; }

        [JsonPropertyName("submitted_at")]
        public string SubmittedAt { get; set; } = "";
    }

    public class LeaderboardJsonEntry
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("max_combo")]
        public int MaxCombo { get; set; }

        [JsonPropertyName("hits_300")]
        public int Hits300 { get; set; }

        [JsonPropertyName("hits_200")]
        public int Hits200 { get; set; }

        [JsonPropertyName("hits_50")]
        public int Hits50 { get; set; }

        [JsonPropertyName("misses")]
        public int Misses { get; set; }

        [JsonPropertyName("submitted_at")]
        public string SubmittedAt { get; set; } = "";
    }

    public class LeaderboardResponse
    {
        [JsonPropertyName("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "";

        [JsonPropertyName("leaderboard")]
        public List<LeaderboardJsonEntry> Leaderboard { get; set; } = new();
    }

    public class ApiError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }
}
