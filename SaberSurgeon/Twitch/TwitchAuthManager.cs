using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SaberSurgeon.Twitch
{
    public class TwitchAuthManager
    {
        // Your backend base URL (no trailing slash)
        private const string BackendBaseUrl = "https://phoenixblaze0.duckdns.org";

        // Twitch application client ID (used for refresh)
        private const string ClientId = "uuqu09tvz1910s5zzz6p9f81ex648b";

        // Used by TwitchApiClient to resolve your support channel
        public const string SupportChannelName = "phoenixblaze0";

        private static TwitchAuthManager _instance;
        public static TwitchAuthManager Instance => _instance ?? (_instance = new TwitchAuthManager());

        private string _accessToken;
        private string _refreshToken;

        // Simple per‑machine encryption for stored tokens
        private readonly byte[] _entropy =
            Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier.Substring(0, 16));

        /// <summary>
        /// True if there is a non‑empty access token and its cached expiry has not passed.
        /// </summary>
        public bool IsAuthenticated =>
            !string.IsNullOrEmpty(_accessToken) &&
            DateTime.UtcNow.Ticks < Plugin.Settings.TokenExpiryTicks;

        /// <summary>
        /// Load tokens from config and, if valid, auto‑connect and refresh supporter info.
        /// Call this once from Plugin.Init / OnEnable.
        /// </summary>
        public void Initialize()
        {
            LoadTokens();

            if (IsAuthenticated)
            {
                Plugin.Log.Info("TwitchAuth: Found cached tokens. Auto-connecting...");
                Plugin.Settings.BackendStatus = "Connected";

                // Optional: background refresh + supporter info update
                _ = Task.Run(async () =>
                {
                    await RefreshTokenIfNeeded();
                    await TwitchApiClient.Instance.FetchBroadcasterAndSupportInfo();
                });
            }
            else
            {
                Plugin.Settings.BackendStatus = "Not connected";
            }
        }

        /// <summary>
        /// Starts the browser-based login via your Node backend (/login).
        /// Then polls /token until it returns access + refresh tokens.
        /// </summary>
        public async Task InitiateLogin()
        {
            try
            {
                string state = Guid.NewGuid().ToString("N");
                Plugin.Log.Info($"TwitchAuth: Opening backend login with state={state}");
                Plugin.Settings.BackendStatus = "Opening browser...";

                string loginUrl = $"{BackendBaseUrl}/login?state={Uri.EscapeDataString(state)}";
                Application.OpenURL(loginUrl);

                await PollForBackendToken(state);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error("TwitchAuth: InitiateLogin (backend) failed: " + ex.Message);
                Plugin.Settings.BackendStatus = "Login failed";
            }
        }

        /// <summary>
        /// Poll your backend /token?state=... until it returns tokens or times out.
        /// Backend is expected to send JSON like: { "access_token": "...", "refresh_token": "..." }.
        /// </summary>
        private async Task PollForBackendToken(string state)
        {
            if (string.IsNullOrEmpty(state))
                return;

            try
            {
                using (var client = new HttpClient())
                {
                    int attempts = 0;

                    // Up to ~3 minutes (90 * 2s)
                    while (attempts < 90)
                    {
                        attempts++;

                        string url = $"{BackendBaseUrl}/token?state={Uri.EscapeDataString(state)}";
                        HttpResponseMessage resp = await client.GetAsync(url);
                        string jsonText = await resp.Content.ReadAsStringAsync();

                        if (resp.IsSuccessStatusCode)
                        {
                            var json = JObject.Parse(jsonText);
                            _accessToken = json["access_token"]?.ToString();
                            _refreshToken = json["refresh_token"]?.ToString();

                            if (string.IsNullOrEmpty(_accessToken))
                            {
                                Plugin.Log.Error("TwitchAuth: Backend /token missing access_token");
                                Plugin.Settings.BackendStatus = "Token error";
                                return;
                            }

                            // Backend currently doesn't send expires_in, so assume ~4 hours.
                            Plugin.Settings.TokenExpiryTicks =
                                DateTime.UtcNow.AddHours(4).Ticks;

                            SaveTokens();


                            Plugin.Log.Info("TwitchAuth: Tokens received, IsAuthenticated=" + IsAuthenticated);
                            Plugin.Log.Info("TwitchAuth: Backend authorization successful!");
                            Plugin.Settings.BackendStatus = "Connected";

                            // Fetch broadcaster + supporter tier to phoenixblaze0
                            _ = TwitchApiClient.Instance.FetchBroadcasterAndSupportInfo();

                            return;
                        }

                        // 404 from backend means "still pending"
                        if (resp.StatusCode == HttpStatusCode.NotFound)
                        {
                            await Task.Delay(2000);
                            continue;
                        }

                        // Any other status is treated as an error
                        Plugin.Log.Error($"TwitchAuth: Backend /token HTTP {resp.StatusCode}: {jsonText}");
                        Plugin.Settings.BackendStatus = "Token error";
                        return;
                    }

                    Plugin.Log.Warn("TwitchAuth: /token polling timed out");
                    Plugin.Settings.BackendStatus = "Login timeout";
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error("TwitchAuth: PollForBackendToken exception: " + ex.Message);
                Plugin.Settings.BackendStatus = "Login error";
            }
        }

        /// <summary>
        /// If token is close to expiring and a refresh token exists, refresh against Twitch directly.
        /// </summary>
        private async Task RefreshTokenIfNeeded()
        {
            // Only refresh when within 5 minutes of expiry
            if (DateTime.UtcNow.AddMinutes(5).Ticks <= Plugin.Settings.TokenExpiryTicks)
                return;

            if (string.IsNullOrEmpty(_refreshToken))
                return;

            try
            {
                Plugin.Log.Info("TwitchAuth: Refreshing token...");

                using (var client = new HttpClient())
                {
                    var values = new[]
                    {
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("refresh_token", _refreshToken)
                    };

                    var content = new FormUrlEncodedContent(values);
                    HttpResponseMessage response =
                        await client.PostAsync("https://id.twitch.tv/oauth2/token", content);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        ParseAndSaveTokens(responseString);
                        Plugin.Log.Info("TwitchAuth: Refresh successful");
                    }
                    else
                    {
                        Plugin.Log.Error("TwitchAuth: Refresh failed: " + responseString);
                        Plugin.Settings.BackendStatus = "Refresh failed";
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error("TwitchAuth: RefreshTokenIfNeeded exception: " + ex.Message);
                Plugin.Settings.BackendStatus = "Refresh error";
            }
        }

        /// <summary>
        /// Parse standard Twitch token JSON (includes expires_in) and update local cache.
        /// Used by the refresh path.
        /// </summary>
        private void ParseAndSaveTokens(string jsonResponse)
        {
            var json = JObject.Parse(jsonResponse);

            _accessToken = json["access_token"]?.ToString();
            _refreshToken = json["refresh_token"]?.ToString();

            int expiresIn = json["expires_in"]?.Value<int>() ?? 3600;
            Plugin.Settings.TokenExpiryTicks =
                DateTime.UtcNow.AddSeconds(expiresIn).Ticks;

            SaveTokens();
        }

        /// <summary>
        /// Encrypt and persist tokens into Plugin.Settings.
        /// </summary>
        private void SaveTokens()
        {
            Plugin.Settings.EncryptedAccessToken = EncryptString(_accessToken);
            Plugin.Settings.EncryptedRefreshToken = EncryptString(_refreshToken);
        }

        /// <summary>
        /// Load and decrypt tokens from Plugin.Settings.
        /// </summary>
        private void LoadTokens()
        {
            _accessToken = DecryptString(Plugin.Settings.EncryptedAccessToken);
            _refreshToken = DecryptString(Plugin.Settings.EncryptedRefreshToken);
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Used by TwitchApiClient to call Helix endpoints.
        /// </summary>
        public string GetAccessToken() => _accessToken;
    }
}
