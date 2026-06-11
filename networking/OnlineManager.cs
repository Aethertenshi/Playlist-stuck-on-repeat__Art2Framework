using System;
using System.Threading.Tasks;

namespace CoreGame
{
    /// <summary>
    /// High-level facade over ApiClient that the game code calls directly.
    /// Handles fire-and-forget score submission, login/register state, 
    /// and thread-safe status messages for the UI to display.
    /// </summary>
    public class OnlineManager
    {
        private readonly ApiClient _api;

        // ─── Public State (read by UI) ───────────────────────────────────────
        public bool IsLoggedIn => _api.IsLoggedIn;
        public string Username => _api.Username;
        public string UserId => _api.UserId;

        /// <summary>Latest status message for the UI to display (e.g. "Score submitted!", "Login failed").</summary>
        public string StatusMessage { get; private set; } = "";

        /// <summary>True while an async operation is in progress.</summary>
        public bool IsBusy { get; private set; }

        public OnlineManager(string apiBaseUrl)
        {
            _api = new ApiClient(apiBaseUrl);
        }

        // ─── Auth ────────────────────────────────────────────────────────────

        /// <summary>Fire-and-forget login. Updates StatusMessage when done.</summary>
        public void Login(string email, string password)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Logging in...";

            Task.Run(async () =>
            {
                var (ok, msg) = await _api.LoginAsync(email, password);
                StatusMessage = msg;
                IsBusy = false;
            });
        }

        /// <summary>Fire-and-forget register. Updates StatusMessage when done.</summary>
        public void Register(string email, string password, string username)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Registering...";

            Task.Run(async () =>
            {
                var (ok, msg) = await _api.RegisterAsync(email, password, username);
                StatusMessage = msg;
                IsBusy = false;
            });
        }

        /// <summary>Log out locally.</summary>
        public void Logout()
        {
            _api.Logout();
            StatusMessage = "Logged out";
        }

        // ─── Score Submission ────────────────────────────────────────────────

        /// <summary>
        /// Submit a score in the background. Does not block the game thread.
        /// Call this from ResultScreen.Show or from the gameplay finish handler.
        /// </summary>
        public void SubmitScore(int beatmapId, int score, int maxCombo,
                                int hits300, int hits200, int hits50, int misses,
                                GameplayMode mode, string difficulty)
        {
            if (!IsLoggedIn)
            {
                StatusMessage = "Not logged in — score not submitted";
                return;
            }

            string modeStr = mode == GameplayMode.Taiko ? "taiko" : "stack";

            Task.Run(async () =>
            {
                var (ok, msg) = await _api.SubmitScoreAsync(
                    beatmapId, score, maxCombo,
                    hits300, hits200, hits50, misses,
                    modeStr, difficulty);

                StatusMessage = msg;

                if (ok)
                    Console.WriteLine($"[Online] Score submitted: {score} on beatmap {beatmapId}");
                else
                    Console.WriteLine($"[Online] Score submit failed: {msg}");
            });
        }

        // ─── Leaderboard (async, returns data) ──────────────────────────────

        /// <summary>
        /// Fetch leaderboard for a beatmap. This is awaitable for when you build the leaderboard UI.
        /// </summary>
        public async Task<LeaderboardResponse?> GetLeaderboardAsync(int beatmapId, GameplayMode mode, int limit = 50)
        {
            string modeStr = mode == GameplayMode.Taiko ? "taiko" : "stack";
            var (ok, data, msg) = await _api.GetLeaderboardAsync(beatmapId, modeStr, limit);

            if (!ok)
                Console.WriteLine($"[Online] Leaderboard fetch failed: {msg}");

            return data;
        }
    }
}
