using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreGame
{
    /// <summary>
    /// Lightweight HTTP client wrapper for the PSoR score API.
    /// Handles auth state, token storage (in-memory), and all API calls.
    /// All public methods are async and return (success, result/error).
    /// </summary>
    public class ApiClient
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _baseUrl;

        // ─── Auth State (in-memory only) ─────────────────────────────────────
        public bool IsLoggedIn { get; private set; }
        public string UserId { get; private set; } = "";
        public string Username { get; private set; } = "";
        public string Token { get; private set; } = "";
        public string RefreshToken { get; private set; } = "";

        /// <summary>
        /// Creates a new ApiClient pointing at the given base URL.
        /// Example: new ApiClient("http://192.168.1.53:3000")
        /// </summary>
        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        // ─── Auth ────────────────────────────────────────────────────────────

        /// <summary>Register a new account.</summary>
        public async Task<(bool ok, string message)> RegisterAsync(string email, string password, string username)
        {
            try
            {
                var body = new RegisterRequest { Email = email, Password = password, Username = username };
                var response = await PostAsync<RegisterResponse>("/auth/register", body);

                if (response != null)
                    return (true, $"Account created: {response.Username}");
            }
            catch (ApiException ex)
            {
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }

            return (false, "Unknown error");
        }

        /// <summary>Log in and store the JWT token in memory.</summary>
        public async Task<(bool ok, string message)> LoginAsync(string email, string password)
        {
            try
            {
                var body = new LoginRequest { Email = email, Password = password };
                var response = await PostAsync<LoginResponse>("/auth/login", body);

                if (response != null)
                {
                    Token = response.Token;
                    RefreshToken = response.RefreshToken;
                    UserId = response.UserId;
                    Username = response.Username;
                    IsLoggedIn = true;

                    return (true, $"Logged in as {response.Username}");
                }
            }
            catch (ApiException ex)
            {
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }

            return (false, "Unknown error");
        }

        /// <summary>Refresh the access token using the stored refresh token.</summary>
        public async Task<bool> RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken)) return false;

            try
            {
                var body = new { refresh_token = RefreshToken };
                var response = await PostAsync<RefreshResponse>("/auth/refresh", body);

                if (response != null)
                {
                    Token = response.Token;
                    RefreshToken = response.RefreshToken;
                    return true;
                }
            }
            catch
            {
                // Refresh failed — user needs to re-login
            }

            return false;
        }

        /// <summary>Clear auth state (log out locally).</summary>
        public void Logout()
        {
            IsLoggedIn = false;
            Token = "";
            RefreshToken = "";
            UserId = "";
            Username = "";
        }

        // ─── Scores ─────────────────────────────────────────────────────────

        /// <summary>Submit a score. Requires login. Fires-and-forgets internally on failure.</summary>
        public async Task<(bool ok, string message)> SubmitScoreAsync(
            int beatmapId, int score, int maxCombo,
            int hits300, int hits200, int hits50, int misses,
            string gameplayMode, string difficulty)
        {
            if (!IsLoggedIn)
                return (false, "Not logged in");

            try
            {
                var body = new ScoreSubmitRequest
                {
                    BeatmapId = beatmapId,
                    Score = score,
                    MaxCombo = maxCombo,
                    Hits300 = hits300,
                    Hits200 = hits200,
                    Hits50 = hits50,
                    Misses = misses,
                    GameplayMode = gameplayMode,
                    Difficulty = difficulty
                };

                var response = await PostAuthAsync<ScoreSubmitResponse>("/scores/submit", body);

                if (response != null)
                    return (true, "Score submitted!");
            }
            catch (ApiException ex) when (ex.StatusCode == 401)
            {
                // Token expired — try refresh once
                if (await RefreshTokenAsync())
                    return await SubmitScoreAsync(beatmapId, score, maxCombo, hits300, hits200, hits50, misses, gameplayMode, difficulty);
                return (false, "Session expired, please re-login");
            }
            catch (ApiException ex)
            {
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }

            return (false, "Unknown error");
        }

        /// <summary>Get the leaderboard for a beatmap.</summary>
        public async Task<(bool ok, LeaderboardResponse? data, string message)> GetLeaderboardAsync(int beatmapId, string mode = "taiko", int limit = 50)
        {
            try
            {
                var url = $"/scores/{beatmapId}?mode={mode}&limit={limit}";
                var response = await GetAsync<LeaderboardResponse>(url);
                return (true, response, "OK");
            }
            catch (ApiException ex)
            {
                return (false, null, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, null, $"Connection error: {ex.Message}");
            }
        }

        // ─── Internal HTTP Helpers ──────────────────────────────────────────

        private async Task<T?> PostAsync<T>(string path, object body) where T : class
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_baseUrl}{path}", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryParseError(responseBody);
                throw new ApiException(error, (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
        }

        private async Task<T?> PostAuthAsync<T>(string path, object body) where T : class
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            var response = await _http.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryParseError(responseBody);
                throw new ApiException(error, (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
        }

        private async Task<T?> GetAsync<T>(string path) where T : class
        {
            var response = await _http.GetAsync($"{_baseUrl}{path}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryParseError(responseBody);
                throw new ApiException(error, (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
        }

        private static string TryParseError(string responseBody)
        {
            try
            {
                var err = JsonSerializer.Deserialize<ApiError>(responseBody, _jsonOptions);
                return err?.Error ?? responseBody;
            }
            catch
            {
                return responseBody;
            }
        }
    }

    /// <summary>Thrown when the API returns a non-2xx status code.</summary>
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
